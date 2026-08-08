using C4iSytemsMobApp.Data;
using C4iSytemsMobApp.Data.Entity;
using Microsoft.EntityFrameworkCore;

namespace C4iSytemsMobApp.Services.Tracking
{
    /// <summary>
    /// The mode state machine and adaptive sampler (ADD §5, §6.2). One instance per app.
    ///
    /// Life rules, in priority order:
    ///  - No server session ⇒ no sampling, ever. Start is refused server-side for units
    ///    without enrolment + consent, and this class never captures a fix outside a session.
    ///  - Duress > Live > Transit > Normal. Mode changes arrive on the ingest response
    ///    (authoritative) — a silent push only makes the next poll happen sooner.
    ///  - Normal mode captures NOTHING here: the NFC scan path already carries GPS and the
    ///    server derives the anchor from the scan event. Zero extra battery by default.
    ///  - Every point is persisted to SQLite BEFORE upload and deleted only on confirmed
    ///    acceptance; unsent points ride the existing SyncService as backfill.
    /// </summary>
    public class TrackingService
    {
        private const int RingBufferCap = 10_000;          // a full shift fully offline
        private const int MaxBatch = 200;

        private static readonly Lazy<TrackingService> _instance = new(() => new TrackingService());
        public static TrackingService Instance => _instance.Value;

        private readonly TrackingApiService _api = new();
        private Func<AppDbContext>? _dbFactory;

        private CancellationTokenSource? _loop;
        private Guid _sessionId;
        private int _unitId;
        private int _seq;
        private int _commandSeqSeen;
        private byte _mode = 1;                            // Normal
        private TrackingPolicyDto _policy = new();
        private Location? _lastKept;
        private DateTime _lastKeptAtUtc;
        private DateTime _lastUploadUtc;
        private int _stationaryStreak;

        /* Direct-send buffer: if the local SQLite cache is unusable on a device (e.g. a
           legacy app database the migrations cannot upgrade), points queue here and upload
           straight to the API — the same live-first approach the logbook has always used.
           Bounded; oldest sacrificed first. */
        private readonly object _memLock = new();
        private readonly List<TrackingPointCache> _memBuffer = new();

        public bool IsTracking => _loop is { IsCancellationRequested: false };
        public byte CurrentMode => _mode;

        public void Configure(Func<AppDbContext> dbFactory) => _dbFactory = dbFactory;

        /// <summary>Called after a successful guard login on a patrol-car wand. Quietly does
        /// nothing when the unit is not enrolled, consent is missing, or the server has
        /// tracking disabled — the officer's workflow is identical either way.</summary>
        public async Task StartIfEligibleAsync()
        {
            if (IsTracking || _dbFactory == null)
                return;

            var guardId = int.TryParse(Preferences.Get("GuardId", ""), out var g) ? g : 0;
            var siteId = int.TryParse(Preferences.Get("SelectedClientSiteId", ""), out var s) ? s : 0;
            if (guardId <= 0 || siteId <= 0)
                return;

            /* The guard's own login declarations: the "Mobile Patrol Car" toggle and the
               Callsign picker. These beat any server-side guess about what the unit is —
               the same wand may be in a car today and on foot tomorrow. */
            var isPatrolCar = Preferences.Get("IsPatrolCar", false);
            var callsign = Preferences.Get("SelectedCallsign", string.Empty);
            var positionName = Preferences.Get("SelectedPosition", string.Empty);
            var positionId = App.PcarPostionId;

            /* Unit identity. The DEVICE is never the unit — a "SmartWand" record is just a
               registered phone. What is tracked is a car or a person:
                   patrol car -> the Position picked at login
                   foot guard -> the guard themselves
               Offsets keep the two apart and must match TrackingUnitKey on the server. */
            const int PositionUnitOffset = 2_000_000;
            const int GuardUnitOffset = 1_000_000;
            var unitId = (isPatrolCar && positionId.HasValue && positionId.Value > 0)
                ? PositionUnitOffset + positionId.Value
                : GuardUnitOffset + guardId;

            var session = await _api.StartSessionAsync(unitId, guardId, siteId, isPatrolCar, callsign,
                positionId, positionName);
            if (session == null)
            {
                Console.WriteLine($"[Tracking] start refused/unreachable for unit {unitId}");
                return;   // not enrolled / no consent / tracking off — by design, silent
            }
            Console.WriteLine($"[Tracking] session {session.SessionId} started, unit {unitId}");

            _sessionId = session.SessionId;
            _unitId = unitId;
            _policy = session.Policy ?? new TrackingPolicyDto();
            /* The server reuses an Active session on re-login, and (unit, session, seq) is a
               dedupe key — restarting seq at 0 would make every new point a silent duplicate.
               Resume the counter where this session left off. */
            _seq = Preferences.Get($"TrackSeq_{session.SessionId}", 0);
            _commandSeqSeen = 0;
            _mode = 2;                                     // a fresh session starts in Transit
            _lastKept = null;
            _stationaryStreak = 0;
            _lastUploadUtc = DateTime.UtcNow;

            _loop = new CancellationTokenSource();
            _ = RunAsync(_loop.Token);

#if ANDROID
            Platforms.TrackingForegroundServiceHelper.Start();
#endif

            /* ---- FIELD SELF-TEST (temporary, 8 Aug 2026): proves the app->API positions
               pipe with NO GPS involved. One fixed synthetic point (9.6700, 76.8100 — Poonjar
               test marker), cached and uploaded immediately. If this row reaches TrackPoint,
               the upload path is healthy and only fix acquisition can be at fault.
               REMOVE once the field issue is closed. */
            try
            {
                Console.WriteLine("[Tracking] self-test: sending synthetic point");
                await KeepAsync(new Location(9.6700, 76.8100) { Timestamp = DateTimeOffset.UtcNow }, "transit");
                await UploadPendingAsync();
                Console.WriteLine("[Tracking] self-test: done (check TrackPoint for 9.67/76.81)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] self-test FAILED: {ex.GetType().Name} {ex.Message}");
            }
        }

        /// <summary>The hard stop (§13.5): called on logout. Flushes what it can, ends the
        /// session, and stops the sampler unconditionally.</summary>
        public async Task StopAsync()
        {
            var loop = _loop;
            _loop = null;
            loop?.Cancel();

#if ANDROID
            Platforms.TrackingForegroundServiceHelper.Stop();
#endif

            if (_sessionId != Guid.Empty)
            {
                try { await UploadPendingAsync(); } catch { /* backfill covers it */ }
                await _api.EndSessionAsync(_sessionId);
                _sessionId = Guid.Empty;
            }
            _mode = 1;
        }

        /// <summary>Duress raised on this device: highest priority, effective immediately,
        /// no waiting for the server round-trip (§5.4).</summary>
        public void EscalateToDuress() => _mode = 4;

        /* ------------------------------ the loop ------------------------------ */

        private async Task RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var intervalSec = await SampleOnceAsync(ct);

                    var uploadDue = (DateTime.UtcNow - _lastUploadUtc).TotalSeconds >=
                                    (_mode >= 3 ? _policy.LiveUploadBatchSec : _policy.UploadBatchSec);
                    if (_mode == 4 || uploadDue)
                        await UploadPendingAsync();

                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, intervalSec)), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    /* A sampler fault must never crash the app; wait a beat and continue. */
                    Console.WriteLine($"[Tracking] loop fault: {ex.GetType().Name} {ex.Message}");
                    try { await Task.Delay(5000, ct); } catch { break; }
                }
            }
        }

        /// <summary>Takes (or skips) one fix and returns the next interval — the §6.2 table.</summary>
        private async Task<int> SampleOnceAsync(CancellationToken ct)
        {
            /* Mode-fixed cadences first. */
            if (_mode == 4)
            {
                await CaptureAsync("duress", ct);
                return _policy.DuressSec;
            }
            if (_mode == 3)
            {
                await CaptureAsync("live", ct);
                return _policy.LiveModeSec;
            }

            /* Transit: adapt to motion. */
            var location = await GetFixAsync(ct);
            if (location == null)
                return _policy.TransitSteadySec;

            var speedKph = (location.Speed ?? 0) * 3.6;
            var movedM = _lastKept == null ? double.MaxValue
                : Location.CalculateDistance(_lastKept, location, DistanceUnits.Kilometers) * 1000;

            /* Distance filter: suppress near-duplicates unless the stationary heartbeat is due. */
            var heartbeatDue = (DateTime.UtcNow - _lastKeptAtUtc).TotalSeconds >= _policy.StationarySec;
            if (movedM < _policy.DistanceFilterM && !heartbeatDue)
            {
                _stationaryStreak++;
                return _stationaryStreak >= 3 ? _policy.StationarySec : _policy.TransitSteadySec;
            }

            _stationaryStreak = movedM < _policy.DistanceFilterM ? _stationaryStreak + 1 : 0;
            await KeepAsync(location, "transit");

            /* Manoeuvring detection: heading swing or speed change sharpens the cadence —
               corners are where a fixed interval loses the road (§6.2). */
            var headingSwing = _lastKept?.Course is { } prev && location.Course is { } cur
                && Math.Abs(((cur - prev + 540) % 360) - 180) > 25;
            if (speedKph > 15 && headingSwing)
                return _policy.TransitManoeuvreSec;
            if (speedKph > 15)
                return _policy.TransitSteadySec;
            return _policy.StationarySec;
        }

        private async Task CaptureAsync(string source, CancellationToken ct)
        {
            var location = await GetFixAsync(ct);
            if (location != null)
                await KeepAsync(location, source);
        }

        private static async Task<Location?> GetFixAsync(CancellationToken ct)
        {
            try
            {
                Location? fix;
                try
                {
                    fix = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.High,
                        Timeout = TimeSpan.FromSeconds(10)
                    }, ct);
                }
                catch (Exception exHigh)
                {
                    Console.WriteLine($"[Tracking] high-accuracy fix failed: {exHigh.GetType().Name} {exHigh.Message}");
                    fix = null;
                }

                /* Indoors / urban canyon a High fix may never lock. Degrade the same way the
                   logbook path does (PermissionService): Medium first, then the platform's
                   cached last-known fix — coarser data beats silence. Accuracy rides along,
                   so the server can still flag what it doesn't trust. */
                fix ??= await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(8)
                }, ct);

                if (fix == null)
                {
                    var cached = await Geolocation.GetLastKnownLocationAsync();
                    if (cached != null && (DateTimeOffset.UtcNow - cached.Timestamp) < TimeSpan.FromMinutes(10))
                    {
                        Console.WriteLine("[Tracking] fix: using last-known (fresh GPS unavailable)");
                        fix = cached;
                    }
                }

                /* Final resort — the LOGBOOK's own source: the last coordinate any part of the
                   app stored. This is exactly what makes logbook pins "always work", so the
                   tracker must never do worse. These points carry no Accuracy value, which is
                   how the server can tell them from real fixes. */
                if (fix == null)
                {
                    var pref = Preferences.Get("GpsCoordinates", "");
                    var parts = pref.Split(',');
                    if (parts.Length == 2
                        && decimal.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var plat)
                        && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var plon))
                    {
                        Console.WriteLine("[Tracking] fix: using logbook cached coordinate");
                        fix = new Location((double)plat, (double)plon) { Timestamp = DateTimeOffset.UtcNow };
                    }
                }

                if (fix == null)
                    Console.WriteLine("[Tracking] fix: null after High+Medium+last-known+cached");
                return fix;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] fix failed: {ex.GetType().Name} {ex.Message}");
                return null;   // no permission / no fix: the gap is honest
            }
        }

        private async Task KeepAsync(Location location, string source)
        {
            if (_dbFactory == null || _sessionId == Guid.Empty)
                return;

            _lastKept = location;
            _lastKeptAtUtc = DateTime.UtcNow;

            /* Battery level is nice-to-have telemetry, never a reason to lose a point:
               on some devices (Samsung A12 in the field) reading it throws
               PermissionException demanding android.permission.BATTERY_STATS — which
               killed EVERY point at creation until 9 Aug 2026. Best-effort only. */
            byte? batteryPct = null;
            try { batteryPct = (byte?)Math.Clamp(Battery.Default.ChargeLevel * 100, 0, 100); }
            catch { /* leave null — the server treats it as unknown */ }

            var point = new TrackingPointCache
            {
                UnitId = _unitId,
                SessionId = _sessionId,
                Seq = ++_seq,
                RecordedUtc = location.Timestamp.UtcDateTime,
                Latitude = (decimal)location.Latitude,
                Longitude = (decimal)location.Longitude,
                AccuracyM = location.Accuracy,
                SpeedKph = location.Speed is { } sp ? sp * 3.6 : null,
                HeadingDeg = location.Course,
                BatteryPct = batteryPct,
                IsMock = location.IsFromMockProvider,
                Source = source
            };
            Preferences.Set($"TrackSeq_{_sessionId}", _seq);

            try
            {
                using var db = _dbFactory();
                db.TrackingPointCache.Add(point);

                /* Ring buffer: the oldest points are the ones sacrificed, never the app. */
                var overflow = await db.TrackingPointCache.CountAsync() - RingBufferCap;
                if (overflow > 0)
                {
                    var oldest = await db.TrackingPointCache.OrderBy(p => p.Id).Take(overflow).ToListAsync();
                    db.TrackingPointCache.RemoveRange(oldest);
                }
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                /* The cache is a means, not the mission (logbook precedent): queue in
                   memory and send direct rather than lose the point. */
                Console.WriteLine($"[Tracking] local cache failed ({ex.GetType().Name}: {ex.Message}); using direct-send buffer");
                lock (_memLock)
                {
                    _memBuffer.Add(point);
                    if (_memBuffer.Count > 1000)
                        _memBuffer.RemoveAt(0);
                }
            }
        }

        /// <summary>Uploads pending points oldest-first and applies the response's
        /// authoritative mode + policy (§5.3). Called by the loop and by SyncService.</summary>
        public async Task UploadPendingAsync()
        {
            if (_dbFactory == null || _sessionId == Guid.Empty)
                return;
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Console.WriteLine($"[Tracking] upload skipped: connectivity = {Connectivity.Current.NetworkAccess}");
                return;
            }

            /* The cache is read tolerantly: a device whose local DB cannot be upgraded
               still uploads from the direct-send buffer (logbook precedent). */
            List<TrackingPointCache> dbBatch = new();
            AppDbContext? db = null;
            try
            {
                db = _dbFactory();
                dbBatch = await db.TrackingPointCache
                    .Where(p => p.SessionId == _sessionId)
                    .OrderBy(p => p.Id)
                    .Take(MaxBatch)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] local cache read failed ({ex.GetType().Name}); sending from memory buffer only");
                db?.Dispose();
                db = null;
            }

            List<TrackingPointCache> memBatch;
            lock (_memLock)
                memBatch = _memBuffer.Where(p => p.SessionId == _sessionId).Take(MaxBatch).ToList();

            try
            {
                var batch = dbBatch.Concat(memBatch).OrderBy(p => p.Seq).Take(MaxBatch).ToList();
                if (batch.Count == 0)
                {
                    _lastUploadUtc = DateTime.UtcNow;
                    return;
                }

                var response = await _api.PostBatchAsync(_unitId, _sessionId, _commandSeqSeen, batch);
                if (response == null)
                {
                    Console.WriteLine($"[Tracking] upload of {batch.Count} point(s) failed; kept for retry");
                    return;   // offline / disabled: points stay buffered; SyncService retries
                }
                Console.WriteLine($"[Tracking] uploaded {batch.Count}, accepted {response.Accepted}, rejected {response.Rejected}");

                _lastUploadUtc = DateTime.UtcNow;

                /* Delete only on confirmed acknowledgement — the offline-cache contract. */
                if (db != null && dbBatch.Count > 0)
                {
                    db.TrackingPointCache.RemoveRange(dbBatch);
                    await db.SaveChangesAsync();
                }
                lock (_memLock)
                    _memBuffer.RemoveAll(p => memBatch.Contains(p));

                /* Authoritative mode delivery (D5). Duress set locally is never downgraded by
                   a stale server view — cancellation must come as a newer command. */
                if (response.CommandSeq > _commandSeqSeen || response.DesiredMode != _mode)
                {
                    if (_mode == 4 && response.CommandSeq <= _commandSeqSeen)
                    {
                        // keep local duress until the server speaks with a newer command
                    }
                    else
                    {
                        _mode = response.DesiredMode;
                        _commandSeqSeen = Math.Max(_commandSeqSeen, response.CommandSeq);
                    }
                }
                if (response.Policy != null)
                    _policy = response.Policy;
            }
            finally
            {
                db?.Dispose();
            }
        }

        /// <summary>Backfill hook for SyncService: marks stale points as backfill and pushes
        /// them. Safe to call with no session (replays whatever is cached).</summary>
        public async Task SyncOfflineBacklogAsync()
        {
            if (_dbFactory == null)
                return;
            try
            {
                using var db = _dbFactory();

                var stale = await db.TrackingPointCache
                    .Where(p => !p.IsBackfill && p.RecordedUtc < DateTime.UtcNow.AddMinutes(-5))
                    .ToListAsync();
                if (stale.Count > 0)
                {
                    stale.ForEach(p => p.IsBackfill = true);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Tracking] backlog sweep skipped: {ex.GetType().Name}");
            }

            if (_sessionId != Guid.Empty)
                await UploadPendingAsync();
        }
    }
}
