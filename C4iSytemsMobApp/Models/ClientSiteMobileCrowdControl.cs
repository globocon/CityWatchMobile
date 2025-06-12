

namespace C4iSytemsMobApp.Models
{
    public class ClientSiteMobileCrowdControl
    {        
        public int Id { get; set; }
        public int ClientSiteId { get; set; }
        public int Tcount { get; set; }
        public int Ccount { get; set; }
        public DateTime? CrowdControlDate { get; set; }
        public DateTime? LastUpdateTime { get; set; }
        public List<ClientSiteMobileCrowdControlGuards>? ClientSiteCrowdControlGuards { get; set; }

    }

    public class ClientSiteMobileCrowdControlGuards
    {        
        public int Id { get; set; }
        public int CrowdControlId { get; set; }
        public int ClientSiteId { get; set; }
        public int UserId { get; set; }
        public int GuardId { get; set; }
        public int Pcount { get; set; }
        public string? Location { get; set; }
        public DateTime? CrowdControlDate { get; set; }
        public DateTime? GuardLastUpdateTime { get; set; }

    }

    public class MobileCrowdControlGuard
    {        
        public int ClientSiteId { get; set; }
        public int UserId { get; set; }
        public int GuardId { get; set; }
        public string? Location { get; set; }
    }

    public class ClientSiteMobileCrowdControlData
    {
        public int ClientSiteId { get; set; }
        public int Count { get; set; }
        public bool AddCount { get; set; }
        public List<ClientSiteMobileCrowdControlGuards>? ClientSiteCrowdControlGuards { get; set; }
    }
}
