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

        public async Task<TrackingSessionStartResponse?> StartSessionAsync(int unitId, int guardId, int clientSiteId)
        {
            try
            {
                var response = await Client.PostAsJsonAsync(Url("session/start"),
                    new { unitId, guardId, clientSiteId, pcarRouteId = (int?)null });
                if (!response.IsSuccessStatusCode)
                    return null;   // 403 = not enrolled / no consent; 404 = tracking disabled
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
