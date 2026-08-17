using System.Net.Http.Json;
using C4iSytemsMobApp.Data.Entity;

namespace C4iSytemsMobApp.Services.Tracking
{
    /// <summary>Wire DTOs matching CityWatch.Tracking's ingest contract (ADD §9.1).</summary>
    public class TrackingSessionStartResponse
    {
        public Guid SessionId { get; set; }
        public DateTime StartedUtc { get; set; }
        public TrackingPolicyDto? Policy { get; set; }
    }

    public class TrackingPolicyDto
    {
        public int TransitSteadySec { get; set; } = 10;
        public int TransitManoeuvreSec { get; set; } = 4;
        public int StationarySec { get; set; } = 60;
        public int OnSiteSec { get; set; } = 30;
        public int ApproachingSiteSec { get; set; } = 5;
        public int LiveModeSec { get; set; } = 3;
        public int DuressSec { get; set; } = 2;
        public int DistanceFilterM { get; set; } = 25;
        public int UploadBatchSec { get; set; } = 60;
        public int LiveUploadBatchSec { get; set; } = 5;
    }

    public class IngestResponseDto
    {
        public int Accepted { get; set; }
        public int Rejected { get; set; }
        public byte DesiredMode { get; set; } = 1;   // 1 Normal 2 Transit 3 Live 4 Duress
        public int CommandSeq { get; set; }
        public int? CommandTtlSeconds { get; set; }
        public TrackingPolicyDto? Policy { get; set; }
        public DateTime ServerUtc { get; set; }
        public int? RetryAfterSeconds { get; set; }
    }

    /// <summary>
    /// HTTP client for the tracking feature pack. Every failure returns null/false — the
    /// caller treats an unreachable or tracking-disabled server (404) as a normal offline
    /// condition and keeps buffering locally. There is NO scenario where this service throws
    /// into the patrol workflow.
    /// </summary>
    public class TrackingApiService
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

        private static string Url(string path) => $"{AppConfig.ApiBaseUrl}tracking/{path}";

        public async Task<TrackingSessionStartResponse?> StartSessionAsync(
            int unitId, int guardId, int clientSiteId, bool isPatrolCar, string? callsign,
            int? positionId, string? positionName)
        {
            try
            {
                /* All from the guard's own login screen. Position is THE CAR ("Mobile Patrols
                   (Car) M1") and is the tracked unit's identity — several cars of one fleet
                   roam the same sites at once and all scan the same site tags, so the car is
                   what tells them apart. Callsign is its radio label. */
                var response = await Client.PostAsJsonAsync(Url("session/start"),
                    new { unitId, guardId, clientSiteId, pcarRouteId = (int?)null, isPatrolCar, callsign,
                          positionId, positionName });
                if (!response.IsSuccessStatusCode)
                {
                    /* 403 = this unit is not enrolled / has no consent recorded — a settled
                       answer, not a hiccup. 404 = tracking is switched off server-side.
                       Either way the app behaves identically for the officer: it simply does
                       not track, silently. Logged so a field problem is diagnosable. */
                    Console.WriteLine(
                        $"[Tracking] session/start refused: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<TrackingSessionStartResponse>();
            }
            catch
            {
                return null;
            }
        }

        public async Task EndSessionAsync(Guid sessionId)
        {
            try
            {
                await Client.PostAsJsonAsync(Url("session/end"), new { sessionId });
            }
            catch
            {
                /* The server closes it anyway: logout publishes OfficerLoggedOut, and the
                   session reaper covers the fully-offline case. */
            }
        }

        /// <summary>Registers this phone's FCM token as the server's nudge address for the
        /// unit. The server refuses unless (unitId, sessionId) names the ACTIVE session —
        /// a device can only register for the unit it is signed into.</summary>
        public async Task<bool> RegisterDeviceTokenAsync(int unitId, Guid sessionId, string token, string platform)
        {
            try
            {
                var response = await Client.PostAsJsonAsync(Url("device-token"),
                    new { unitId, sessionId, token, platform });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;   // offline / tracking off: the next login registers again
            }
        }

        /// <summary>Logout: deactivates the nudge address. Best-effort — a failed release
        /// never blocks logout, and a dead token is also retired server-side when FCM
        /// reports it unregistered.</summary>
        public async Task ReleaseDeviceTokenAsync(string token)
        {
            try
            {
                await Client.PostAsJsonAsync(Url("device-token/release"), new { token });
            }
            catch
            {
                /* logout continues regardless */
            }
        }

        public async Task<IngestResponseDto?> PostBatchAsync(int unitId, Guid sessionId, int commandSeqSeen,
            List<TrackingPointCache> points)
        {
            try
            {
                var response = await Client.PostAsJsonAsync(Url("positions"), new
                {
                    unitId,
                    sessionId,
                    deviceUtc = DateTime.UtcNow,
                    commandSeqSeen,
                    points = points.Select(p => new
                    {
                        seq = p.Seq,
                        utc = p.RecordedUtc,
                        lat = p.Latitude,
                        lon = p.Longitude,
                        accuracyM = p.AccuracyM,
                        speedKph = p.SpeedKph,
                        headingDeg = p.HeadingDeg,
                        batteryPct = p.BatteryPct,
                        isMock = p.IsMock,
                        source = p.Source,
                        backfilled = p.IsBackfill
                    }).ToList()
                });
                if (!response.IsSuccessStatusCode)
                    return null;
                return await response.Content.ReadFromJsonAsync<IngestResponseDto>();
            }
            catch
            {
                return null;
            }
        }
    }
}
