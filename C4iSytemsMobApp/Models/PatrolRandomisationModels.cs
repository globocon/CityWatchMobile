namespace C4iSytemsMobApp.Models
{
    /// <summary>
    /// Patrol Randomisation as the server sends it (CityWatch.Web PatrolRandomisationInstruction):
    /// in the scan response, from Scanner/GetPatrolRandomisationStatus and from
    /// Scanner/PatrolRandomisationBypass.
    /// </summary>
    public class PatrolRandomisationInstruction
    {
        public int ClientSiteId { get; set; }
        public int GuardId { get; set; }
        public bool IsConfigured { get; set; }
        public bool IsActive { get; set; }
        public DateTime? WindowStartLocal { get; set; }
        public DateTime? WindowEndLocal { get; set; }
        public DateTime? WindowEndUtc { get; set; }
        public string ScheduleDescription { get; set; }
        public List<PatrolRandomisationWindowUtc> UpcomingWindowsUtc { get; set; } = new();
        public List<string> PriorityTagUIds { get; set; } = new();
        public PatrolRandomisationCheckpoint NextCheckpoint { get; set; }
        public string PreviousCheckpointResult { get; set; }
        public string PreviousCheckpointLabel { get; set; }
        public string Message { get; set; }
    }

    public class PatrolRandomisationWindowUtc
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }

    public class PatrolRandomisationCheckpoint
    {
        public int AssignmentId { get; set; }
        public int TagId { get; set; }
        public string TagUId { get; set; }
        public string Label { get; set; }
        public int TagsTypeId { get; set; }
        public DateTime AssignedUtc { get; set; }
    }

    public class PatrolRandomisationBypassResponse
    {
        public bool IsSuccess { get; set; }
        public string message { get; set; }
        public PatrolRandomisationInstruction PatrolRandomisation { get; set; }
    }

    /// <summary>Body of Scanner/PatrolRandomisationBypass (server: PatrolRandomisationBypassRequest).</summary>
    public class PatrolRandomisationBypassRequest
    {
        public int ClientSiteId { get; set; }
        public int GuardId { get; set; }
        public int UserId { get; set; }
        public int? SmartWandId { get; set; }
        public string TagUId { get; set; }
        public DateTime? TagAssignedUtc { get; set; }
        public DateTime BypassUtc { get; set; }
        public bool IsOffline { get; set; }
        public string Gps { get; set; }
        public string NextTagUId { get; set; }
        public DateTime? NextAssignedUtc { get; set; }
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
    }

    /// <summary>What an offline strike carries to the server about randomisation (keyed by the cache row's UniqueRecordId).</summary>
    public class PatrolRandomisationOfflineScanInfo
    {
        public string ExpectedTagUId { get; set; }
        public DateTime? ExpectedAssignedUtc { get; set; }
        public string NextTagUId { get; set; }
        public DateTime? NextAssignedUtc { get; set; }
    }
}
