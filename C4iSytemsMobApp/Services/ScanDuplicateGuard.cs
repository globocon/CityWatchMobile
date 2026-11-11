using C4iSytemsMobApp.Enums;

namespace C4iSytemsMobApp.Services
{
    /// <summary>
    /// P1#375: one place that decides whether a tag scan is allowed to proceed.
    ///
    /// Two separate problems are solved here, and they need different mechanisms:
    ///
    /// 1. The same tag may only be logged once per <see cref="DuplicateWindow"/> (5 minutes,
    ///    was 1 minute). A guard walking a tour passes the same point repeatedly and NFC/BLE
    ///    readers re-fire on every pass, so the window is what keeps the log readable.
    ///
    /// 2. The same scan must never be sent to the server twice. A single physical tap can
    ///    raise several reader events milliseconds apart, and the server round trip takes far
    ///    longer than that, so the "last accepted" timestamp alone does not help: every one of
    ///    those events reads the same stale timestamp and they all pass. An in-flight flag,
    ///    set atomically before the send starts, is what closes that gap, and it is set BEFORE
    ///    the online/offline branch so both paths are covered.
    ///
    /// State is per app run. Across restarts the offline path still has its own database check
    /// (unsynced cache rows) and the server still has its own, so nothing is left unguarded.
    /// </summary>
    public static class ScanDuplicateGuard
    {
        /// <summary>
        /// How long the same tag stays blocked after an accepted scan. P1#375 raised this from
        /// 1 minute to 5. The offline cache check in ScannerControlServices uses the same value,
        /// so the two paths can never disagree about what counts as a duplicate.
        /// </summary>
        public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(5);

        private static readonly object _sync = new object();

        private static readonly Dictionary<string, TagScanState> _tagStates =
            new Dictionary<string, TagScanState>(StringComparer.OrdinalIgnoreCase);

        /* BLE hands us every beacon it hears, known tag or not, and a shift runs for hours -
           so entries that can no longer refuse anything are swept once the table grows past
           this. Entries still inside the window, or in flight, are always kept. */
        private const int PruneThreshold = 256;

        private sealed class TagScanState
        {
            public bool IsInFlight { get; set; }
            public DateTime? LastAcceptedUtc { get; set; }
        }

        public enum ScanGateResult
        {
            /// <summary>Caller owns the scan and must call Commit or Abandon when it finishes.</summary>
            Allowed,

            /// <summary>The same tag was accepted less than <see cref="DuplicateWindow"/> ago.</summary>
            Duplicate,

            /// <summary>The same tag is already being recorded right now (the milliseconds case).</summary>
            InProgress
        }

        private static string BuildKey(int clientSiteId, string tagUid)
            => $"{clientSiteId}|{(tagUid ?? string.Empty).Trim()}";

        /// <summary>
        /// Claims the right to record this scan. Returns Allowed at most once per tag per
        /// window, and only to one caller at a time. Every Allowed MUST be followed by
        /// <see cref="Commit"/> (record kept) or <see cref="Abandon"/> (nothing kept), or the
        /// tag stays blocked for the rest of the app run.
        /// </summary>
        public static ScanGateResult TryBeginScan(int clientSiteId, string tagUid)
        {
            if (string.IsNullOrWhiteSpace(tagUid))
                return ScanGateResult.Allowed;

            var key = BuildKey(clientSiteId, tagUid);
            var nowUtc = DateTime.UtcNow;

            lock (_sync)
            {
                if (!_tagStates.TryGetValue(key, out var state))
                {
                    state = new TagScanState();
                    _tagStates[key] = state;
                }

                /* Checked first: a second reader event arriving while the first send is still
                   open is the duplicate-row bug, and it has no timestamp to fail on yet. */
                if (state.IsInFlight)
                    return ScanGateResult.InProgress;

                if (state.LastAcceptedUtc.HasValue && (nowUtc - state.LastAcceptedUtc.Value) < DuplicateWindow)
                    return ScanGateResult.Duplicate;

                state.IsInFlight = true;
                PruneExpired(nowUtc);
                return ScanGateResult.Allowed;
            }
        }

        /// <summary>Caller must hold <see cref="_sync"/>.</summary>
        private static void PruneExpired(DateTime nowUtc)
        {
            if (_tagStates.Count <= PruneThreshold) return;

            var expired = _tagStates
                .Where(kvp => !kvp.Value.IsInFlight
                              && (!kvp.Value.LastAcceptedUtc.HasValue
                                  || (nowUtc - kvp.Value.LastAcceptedUtc.Value) >= DuplicateWindow))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
                _tagStates.Remove(key);
        }

        /// <summary>
        /// Non-claiming peek: true when a scan of this tag would be refused right now. Used by
        /// the continuous BLE path to stay silent instead of raising a popup it is about to
        /// close again. Never use it in place of <see cref="TryBeginScan"/> - a peek cannot
        /// reserve anything, so two callers can both see false and both proceed.
        /// </summary>
        public static bool IsBlocked(int clientSiteId, string tagUid)
        {
            if (string.IsNullOrWhiteSpace(tagUid)) return false;

            var key = BuildKey(clientSiteId, tagUid);
            var nowUtc = DateTime.UtcNow;

            lock (_sync)
            {
                if (!_tagStates.TryGetValue(key, out var state))
                    return false;

                if (state.IsInFlight)
                    return true;

                return state.LastAcceptedUtc.HasValue && (nowUtc - state.LastAcceptedUtc.Value) < DuplicateWindow;
            }
        }

        /// <summary>Scan was recorded (server accepted it, or it went to the offline cache).</summary>
        public static void Commit(int clientSiteId, string tagUid)
        {
            if (string.IsNullOrWhiteSpace(tagUid)) return;

            var key = BuildKey(clientSiteId, tagUid);
            lock (_sync)
            {
                if (!_tagStates.TryGetValue(key, out var state))
                {
                    state = new TagScanState();
                    _tagStates[key] = state;
                }

                state.IsInFlight = false;
                state.LastAcceptedUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Scan was not recorded (network failure, rejected tag, exception). The window is not
        /// started, so the guard can re-present the same tag immediately and have it work.
        /// </summary>
        public static void Abandon(int clientSiteId, string tagUid)
        {
            if (string.IsNullOrWhiteSpace(tagUid)) return;

            var key = BuildKey(clientSiteId, tagUid);
            lock (_sync)
            {
                if (_tagStates.TryGetValue(key, out var state))
                    state.IsInFlight = false;
            }
        }

        /// <summary>
        /// Carries a scan time this app run did not see - the offline cache still holding an
        /// unsynced hit from a previous run - into the in-memory window, so a restart does not
        /// reopen the 5 minutes.
        /// </summary>
        public static void RememberAcceptedScan(int clientSiteId, string tagUid, DateTime acceptedUtc)
        {
            if (string.IsNullOrWhiteSpace(tagUid)) return;

            var key = BuildKey(clientSiteId, tagUid);
            lock (_sync)
            {
                if (!_tagStates.TryGetValue(key, out var state))
                {
                    state = new TagScanState();
                    _tagStates[key] = state;
                }

                if (!state.LastAcceptedUtc.HasValue || acceptedUtc > state.LastAcceptedUtc.Value)
                    state.LastAcceptedUtc = acceptedUtc;
            }
        }

        /// <summary>Wording the guard sees, which differs for a tag and an iBeacon.</summary>
        public static string BuildRejectionMessage(ScanGateResult result, ScanningType scannerType)
        {
            var subject = scannerType == ScanningType.BLUETOOTH ? "iBeacon" : "Tag";

            return result == ScanGateResult.InProgress
                ? $"{subject} scan already in progress !!!"
                : $"{subject} already scanned !!!";
        }
    }
}
