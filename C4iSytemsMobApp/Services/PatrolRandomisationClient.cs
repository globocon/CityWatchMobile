using C4iSytemsMobApp.Data.DbServices;
using C4iSytemsMobApp.Data.Entity;
using C4iSytemsMobApp.Helpers;
using C4iSytemsMobApp.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Net.Http.Json;
using System.Text.Json;

namespace C4iSytemsMobApp.Services
{
    /// <summary>
    /// Patrol Randomisation on the handset (server: CityWatch.Web PatrolRandomisationService).
    ///
    /// During a programmed window the header shows "GoTo: next checkpoint" and "Tour: RNDM" instead of
    /// the tag counters; outside it the header is the normal STND one. Each strike is acknowledged
    /// and answered with another random checkpoint. Tapping "Tour: RNDM" bypasses the checkpoint on
    /// screen (missing, faulty or blocked) and the server writes "RNDM Bypass hit by guard X.X" to the
    /// logbook.
    ///
    /// Online, the server picks every checkpoint (weighted towards points missed in the last 72
    /// hours). Offline, this class picks them itself the same way from the local tag list, records
    /// what it showed against each cached strike, and queues bypasses; the sync sends both so the
    /// server's scan log matches what the guard actually did.
    ///
    /// Nothing here touches App.TourMode or the existing scan handling: it only reads scan results
    /// and adds to what is cached and synced, so STND/PCAR/INSP behave exactly as before.
    /// State lives in Preferences so it survives page changes and app restarts.
    /// </summary>
    public static class PatrolRandomisationClient
    {
        private const string StateKey = "PatrolRndm_State";
        private const string ScanInfoKey = "PatrolRndm_OfflineScans";
        private const string BypassQueueKey = "PatrolRndm_BypassQueue";
        private const int PriorityWeight = 3;              // same as the server
        private static readonly TimeSpan ConfigRefreshAge = TimeSpan.FromHours(6);
        private static readonly object _lock = new();
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        /// <summary>Raised whenever what the header should show may have changed.</summary>
        public static event Action StateChanged;

        private class State
        {
            public int SiteId { get; set; }
            public int GuardId { get; set; }
            public bool IsConfigured { get; set; }
            public List<PatrolRandomisationWindowUtc> Windows { get; set; } = new();
            public List<string> Priority { get; set; } = new();
            public DateTime? RefreshedUtc { get; set; }
            public string GoToTagUId { get; set; }
            public string GoToLabel { get; set; }
            public DateTime? GoToAssignedUtc { get; set; }
            public DateTime? DeckWindowStartUtc { get; set; }
            public List<string> Deck { get; set; } = new();
        }

        #region State

        private static State Load()
        {
            try
            {
                var json = Preferences.Get(StateKey, "");
                return string.IsNullOrEmpty(json) ? new State() : JsonSerializer.Deserialize<State>(json, _json) ?? new State();
            }
            catch { return new State(); }
        }

        private static void Save(State s) => Preferences.Set(StateKey, JsonSerializer.Serialize(s));

        private static void RaiseChanged()
        {
            try { StateChanged?.Invoke(); } catch { }
        }

        private static PatrolRandomisationWindowUtc WindowAt(State s, DateTime utc) =>
            s.IsConfigured ? s.Windows?.FirstOrDefault(w => w.StartUtc <= utc && utc < w.EndUtc) : null;

        /// <summary>Starts a fresh deck when a new window begins.</summary>
        private static void EnsureDeck(State s, PatrolRandomisationWindowUtc window)
        {
            if (window == null || s.DeckWindowStartUtc == window.StartUtc) return;
            s.DeckWindowStartUtc = window.StartUtc;
            s.Deck = new List<string>();
        }

        private static void ClearGoTo(State s)
        {
            s.GoToTagUId = null;
            s.GoToLabel = null;
            s.GoToAssignedUtc = null;
        }

        #endregion

        #region What the header shows

        /// <summary>True while a randomisation window is open for this site: show "Tour: RNDM".</summary>
        public static bool IsActive(int siteId)
        {
            lock (_lock)
            {
                var s = Load();
                return s.SiteId == siteId && WindowAt(s, DateTime.UtcNow) != null;
            }
        }

        /// <summary>The "GoTo" checkpoint label, or null when there is none to show.</summary>
        public static string GoToLabel(int siteId)
        {
            lock (_lock)
            {
                var s = Load();
                return s.SiteId == siteId && WindowAt(s, DateTime.UtcNow) != null ? s.GoToLabel : null;
            }
        }

        /// <summary>The GoTo the server should judge a live strike against (sent with the scan).</summary>
        public static string ExpectedTagUId(int siteId)
        {
            lock (_lock)
            {
                var s = Load();
                return s.SiteId == siteId && WindowAt(s, DateTime.UtcNow) != null ? s.GoToTagUId : null;
            }
        }

        #endregion

        #region Online

        /// <summary>Applies what the server sent (scan response, status, bypass) and acknowledges the strike.</summary>
        public static void ApplyServerInstruction(PatrolRandomisationInstruction instruction, bool acknowledge = true)
        {
            if (instruction == null) return;
            string ack = null;

            lock (_lock)
            {
                var s = Load();
                if (s.SiteId != instruction.ClientSiteId || s.GuardId != instruction.GuardId)
                    s = new State { SiteId = instruction.ClientSiteId, GuardId = instruction.GuardId };

                s.IsConfigured = instruction.IsConfigured;
                s.Windows = instruction.UpcomingWindowsUtc ?? new List<PatrolRandomisationWindowUtc>();
                s.Priority = instruction.PriorityTagUIds ?? new List<string>();
                s.RefreshedUtc = DateTime.UtcNow;

                var window = WindowAt(s, DateTime.UtcNow);
                if (!instruction.IsActive || window == null)
                {
                    ClearGoTo(s);
                }
                else
                {
                    EnsureDeck(s, window);
                    if (instruction.NextCheckpoint != null)
                    {
                        s.GoToTagUId = instruction.NextCheckpoint.TagUId;
                        s.GoToLabel = instruction.NextCheckpoint.Label;
                        s.GoToAssignedUtc = instruction.NextCheckpoint.AssignedUtc;
                        if (!s.Deck.Contains(s.GoToTagUId, StringComparer.OrdinalIgnoreCase))
                            s.Deck.Add(s.GoToTagUId);
                    }
                }
                Save(s);

                if (acknowledge && !string.IsNullOrEmpty(instruction.PreviousCheckpointResult))
                    ack = Acknowledgement(instruction.PreviousCheckpointResult, instruction.PreviousCheckpointLabel, s.GoToLabel);
            }

            RaiseChanged();
            if (ack != null) ShowToast(ack);
        }

        /// <summary>Loads the site's windows and the guard's current GoTo. startIfNone hands out a first checkpoint.</summary>
        public static async Task RefreshAsync(int siteId, int guardId, bool startIfNone)
        {
            if (siteId <= 0 || guardId <= 0 || Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            try
            {
                var url = $"{AppConfig.ApiBaseUrl}Scanner/GetPatrolRandomisationStatus?siteId={siteId}&guardId={guardId}&startIfNone={startIfNone.ToString().ToLowerInvariant()}";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;
                var instruction = await response.Content.ReadFromJsonAsync<PatrolRandomisationInstruction>(_json);
                if (instruction == null) return;
                instruction.ClientSiteId = siteId;
                instruction.GuardId = guardId;
                ApplyServerInstruction(instruction, acknowledge: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Patrol randomisation refresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Called every few seconds by the main page: flips the header between RNDM and STND as
        /// windows open and close, fetches (or, offline, picks) the first checkpoint of a window,
        /// and refreshes the window list now and then.
        /// </summary>
        public static async Task TickAsync(int siteId, int guardId, IScanDataDbServices db)
        {
            if (siteId <= 0 || guardId <= 0) return;
            bool online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            bool needRefresh, needFirst, changed = false;

            lock (_lock)
            {
                var s = Load();
                var sameGuard = s.SiteId == siteId && s.GuardId == guardId;
                var window = sameGuard ? WindowAt(s, DateTime.UtcNow) : null;

                needRefresh = !sameGuard || s.RefreshedUtc == null || DateTime.UtcNow - s.RefreshedUtc > ConfigRefreshAge
                              || (s.IsConfigured && !(s.Windows?.Any(w => w.EndUtc > DateTime.UtcNow) ?? false));

                if (sameGuard && window == null && s.GoToTagUId != null)
                {
                    ClearGoTo(s);       // window closed: back to STND
                    Save(s);
                    changed = true;
                }

                needFirst = window != null && s.GoToTagUId == null;
                if (needFirst && !online)
                {
                    // Offline at the start of a window: pick the first checkpoint here.
                    EnsureDeck(s, window);
                    var next = PickNext(s, db?.GetSmartWandTagsOfSite(siteId), null);
                    if (next != null)
                    {
                        SetGoTo(s, next, DateTime.UtcNow);
                        Save(s);
                        changed = true;
                    }
                    needFirst = false;
                }
            }

            if (online && (needRefresh || needFirst))
                await RefreshAsync(siteId, guardId, startIfNone: true);
            else if (changed)
                RaiseChanged();
        }

        /// <summary>
        /// The guard tapped "Tour: RNDM": the checkpoint on screen is missing, faulty or blocked.
        /// Online the server records it, writes the logbook entry and sends another checkpoint;
        /// offline it is queued with its time and another checkpoint is picked here.
        /// </summary>
        public static async Task<string> BypassAsync(int siteId, int guardId, int userId, IScanDataDbServices db)
        {
            PatrolRandomisationBypassRequest request;
            lock (_lock)
            {
                var s = Load();
                if (s.SiteId != siteId || WindowAt(s, DateTime.UtcNow) == null || string.IsNullOrEmpty(s.GoToTagUId))
                    return "There is no checkpoint to bypass right now.";

                request = new PatrolRandomisationBypassRequest
                {
                    ClientSiteId = siteId,
                    GuardId = guardId,
                    UserId = userId,
                    SmartWandId = Preferences.Get($"{siteId}_SavedSmartWandId", 0),
                    TagUId = s.GoToTagUId,
                    TagAssignedUtc = s.GoToAssignedUtc,
                    BypassUtc = DateTime.UtcNow,
                    EventDateTimeLocal = TimeZoneHelper.GetCurrentTimeZoneCurrentTime(),
                    EventDateTimeLocalWithOffset = TimeZoneHelper.GetCurrentTimeZoneCurrentTimeWithOffset(),
                    EventDateTimeZone = TimeZoneHelper.GetCurrentTimeZone(),
                    EventDateTimeZoneShort = TimeZoneHelper.GetCurrentTimeZoneShortName(),
                    EventDateTimeUtcOffsetMinute = TimeZoneHelper.GetCurrentTimeZoneOffsetMinute()
                };
            }

            try { request.Gps = await PermissionService.GetGpsLocationWithOutCheckingPermissionAsync(); } catch { }

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                    var response = await client.PostAsJsonAsync($"{AppConfig.ApiBaseUrl}Scanner/PatrolRandomisationBypass", request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<PatrolRandomisationBypassResponse>(_json);
                        if (result?.PatrolRandomisation != null)
                        {
                            result.PatrolRandomisation.ClientSiteId = siteId;
                            result.PatrolRandomisation.GuardId = guardId;
                            ApplyServerInstruction(result.PatrolRandomisation, acknowledge: false);
                        }
                        var label = GoToLabel(siteId);
                        return label != null ? $"Bypass recorded. GoTo: {label}" : (result?.message ?? "Bypass recorded.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Patrol randomisation bypass failed, queuing it: {ex.Message}");
                }
            }

            // Offline (or the call failed): move on here, send the bypass when the connection is back.
            string goTo;
            lock (_lock)
            {
                var s = Load();
                var window = WindowAt(s, DateTime.UtcNow);
                EnsureDeck(s, window);
                var next = PickNext(s, db?.GetSmartWandTagsOfSite(siteId), request.TagUId);
                if (next != null)
                {
                    SetGoTo(s, next, DateTime.UtcNow);
                    request.NextTagUId = next.UId;
                    request.NextAssignedUtc = s.GoToAssignedUtc;
                }
                else
                {
                    ClearGoTo(s);
                }
                request.IsOffline = true;
                Save(s);
                goTo = s.GoToLabel;

                var queue = LoadList<PatrolRandomisationBypassRequest>(BypassQueueKey);
                queue.Add(request);
                SaveList(BypassQueueKey, queue);
            }
            RaiseChanged();
            return goTo != null ? $"Bypass saved offline. GoTo: {goTo}" : "Bypass saved offline.";
        }

        /// <summary>Sends bypasses made offline, oldest first. Called after the cached strikes have synced.</summary>
        public static async Task FlushOfflineBypassesAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;

            List<PatrolRandomisationBypassRequest> queue;
            lock (_lock) queue = LoadList<PatrolRandomisationBypassRequest>(BypassQueueKey);
            if (queue.Count == 0) return;

            var sent = new List<PatrolRandomisationBypassRequest>();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            foreach (var request in queue.OrderBy(q => q.BypassUtc))
            {
                try
                {
                    // A resend is recognised by the server (same guard, point and time), so retrying is safe.
                    var response = await client.PostAsJsonAsync($"{AppConfig.ApiBaseUrl}Scanner/PatrolRandomisationBypass", request);
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        sent.Add(request);
                    else
                        break;      // keep the order: try the rest next time
                }
                catch
                {
                    break;
                }
            }

            if (sent.Count == 0) return;
            lock (_lock)
            {
                var remaining = LoadList<PatrolRandomisationBypassRequest>(BypassQueueKey)
                    .Where(q => !sent.Any(x => x.BypassUtc == q.BypassUtc && x.TagUId == q.TagUId && x.GuardId == q.GuardId))
                    .ToList();
                SaveList(BypassQueueKey, remaining);
            }
        }

        #endregion

        #region Offline strikes

        /// <summary>
        /// An offline strike was cached. While a window is open: acknowledge it against the GoTo on
        /// screen, pick the next checkpoint here, and remember both against the cache row so the sync
        /// can tell the server. Strikes of other sites' tags are left alone (the server ignores them too).
        /// </summary>
        public static void OnOfflineScan(int siteId, int guardId, string tagUId, DateTime hitUtc, Guid uniqueRecordId, IScanDataDbServices db)
        {
            string ack = null;
            lock (_lock)
            {
                var s = Load();
                if (s.SiteId != siteId || s.GuardId != guardId) return;
                var window = WindowAt(s, hitUtc);
                if (window == null) return;

                var tags = db?.GetSmartWandTagsOfSite(siteId) ?? new List<ClientSiteSmartWandTagsLocal>();
                if (!tags.Any(t => string.Equals(t.UId, tagUId, StringComparison.OrdinalIgnoreCase))) return;

                EnsureDeck(s, window);
                var info = new PatrolRandomisationOfflineScanInfo
                {
                    ExpectedTagUId = s.GoToTagUId,
                    ExpectedAssignedUtc = s.GoToAssignedUtc
                };
                var previousLabel = s.GoToLabel;
                var completed = string.Equals(s.GoToTagUId, tagUId, StringComparison.OrdinalIgnoreCase);

                var next = PickNext(s, tags, tagUId);
                if (next != null)
                {
                    SetGoTo(s, next, hitUtc);
                    info.NextTagUId = next.UId;
                    info.NextAssignedUtc = s.GoToAssignedUtc;
                }
                Save(s);

                var map = LoadMap();
                map[uniqueRecordId.ToString()] = info;
                SaveMap(map);

                if (info.ExpectedTagUId != null)
                    ack = Acknowledgement(completed ? "Completed" : "Skipped", previousLabel, s.GoToLabel);
                else if (s.GoToLabel != null)
                    ack = $"GoTo: {s.GoToLabel}";
            }
            RaiseChanged();
            if (ack != null) ShowToast(ack);
        }

        /// <summary>Copies what the handset showed onto cached strikes about to be synced.</summary>
        public static void EnrichForSync(IEnumerable<ClientSiteSmartWandTagsHitLogCache> records)
        {
            if (records == null) return;
            Dictionary<string, PatrolRandomisationOfflineScanInfo> map;
            lock (_lock) map = LoadMap();
            if (map.Count == 0) return;

            foreach (var r in records)
            {
                if (!map.TryGetValue(r.UniqueRecordId.ToString(), out var info)) continue;
                r.RndmExpectedTagUId = info.ExpectedTagUId;
                r.RndmExpectedAssignedUtc = info.ExpectedAssignedUtc;
                r.RndmNextTagUId = info.NextTagUId;
                r.RndmNextAssignedUtc = info.NextAssignedUtc;
            }
        }

        /// <summary>Drops the notes of strikes the server has accepted.</summary>
        public static void ForgetSynced(IEnumerable<Guid> uniqueRecordIds)
        {
            lock (_lock)
            {
                var map = LoadMap();
                var removed = false;
                foreach (var id in uniqueRecordIds ?? Enumerable.Empty<Guid>())
                    removed |= map.Remove(id.ToString());
                if (removed) SaveMap(map);
            }
        }

        #endregion

        #region Picking

        /// <summary>
        /// Same rules as the server: never the tag just struck (or bypassed), every checkpoint once
        /// per window before any repeats, and points missed in the last 72 hours three times as likely.
        /// </summary>
        private static ClientSiteSmartWandTagsLocal PickNext(State s, List<ClientSiteSmartWandTagsLocal> tags, string excludeUId)
        {
            if (tags == null || tags.Count == 0) return null;

            var candidates = tags
                .Where(t => !string.IsNullOrWhiteSpace(t.UId) && !string.Equals(t.UId, excludeUId, StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.UId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (candidates.Count == 0) return null;

            var deck = new HashSet<string>(s.Deck ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var fresh = candidates.Where(t => !deck.Contains(t.UId)).ToList();
            var pool = fresh.Count > 0 ? fresh : candidates;

            var priority = new HashSet<string>(s.Priority ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var weights = pool.Select(t => priority.Contains(t.UId) ? PriorityWeight : 1).ToList();
            var roll = Random.Shared.Next(weights.Sum());
            for (var i = 0; i < pool.Count; i++)
            {
                if (roll < weights[i]) return pool[i];
                roll -= weights[i];
            }
            return pool[^1];
        }

        private static void SetGoTo(State s, ClientSiteSmartWandTagsLocal tag, DateTime assignedUtc)
        {
            s.GoToTagUId = tag.UId;
            s.GoToLabel = string.IsNullOrWhiteSpace(tag.LabelDescription) ? tag.UId : tag.LabelDescription;
            s.GoToAssignedUtc = DateTime.SpecifyKind(assignedUtc, DateTimeKind.Utc);
            s.Deck ??= new List<string>();
            if (!s.Deck.Contains(tag.UId, StringComparer.OrdinalIgnoreCase))
                s.Deck.Add(tag.UId);
        }

        #endregion

        #region Helpers

        private static string Acknowledgement(string result, string previousLabel, string nextLabel)
        {
            var what = string.IsNullOrEmpty(previousLabel) ? "Checkpoint" : previousLabel;
            var head = result switch
            {
                "Completed" => $"✔ {what} reached.",
                "Skipped" => $"{what} skipped.",
                "Bypassed" => $"{what} bypassed.",
                _ => null
            };
            if (head == null) return null;
            return string.IsNullOrEmpty(nextLabel) ? head : $"{head} GoTo: {nextLabel}";
        }

        private static void ShowToast(string message)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await Toast.Make(message, ToastDuration.Long).Show(); } catch { }
            });
        }

        private static Dictionary<string, PatrolRandomisationOfflineScanInfo> LoadMap()
        {
            try
            {
                var json = Preferences.Get(ScanInfoKey, "");
                return string.IsNullOrEmpty(json)
                    ? new Dictionary<string, PatrolRandomisationOfflineScanInfo>()
                    : JsonSerializer.Deserialize<Dictionary<string, PatrolRandomisationOfflineScanInfo>>(json, _json) ?? new();
            }
            catch { return new Dictionary<string, PatrolRandomisationOfflineScanInfo>(); }
        }

        private static void SaveMap(Dictionary<string, PatrolRandomisationOfflineScanInfo> map) =>
            Preferences.Set(ScanInfoKey, JsonSerializer.Serialize(map));

        private static List<T> LoadList<T>(string key)
        {
            try
            {
                var json = Preferences.Get(key, "");
                return string.IsNullOrEmpty(json) ? new List<T>() : JsonSerializer.Deserialize<List<T>>(json, _json) ?? new List<T>();
            }
            catch { return new List<T>(); }
        }

        private static void SaveList<T>(string key, List<T> list) => Preferences.Set(key, JsonSerializer.Serialize(list));

        #endregion
    }
}
