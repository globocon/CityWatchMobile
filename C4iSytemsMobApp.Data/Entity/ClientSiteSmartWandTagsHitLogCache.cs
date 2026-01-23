using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Data.Entity
{
    public class ClientSiteSmartWandTagsHitLogCache
    {
        [Key]
        public int Id { get; set; }
        public int LoggedInClientSiteId { get; set; }
        public int LoggedInUserId { get; set; }
        public int LoggedInGuardId { get; set; }
        public string TagUId { get; set; }
        public int TagsTypeId { get; set; }        
        public DateTime HitUtcDateTime { get; set; } = DateTime.UtcNow;
        public DateTime HitLocalDateTime { get; set; } = DateTime.Now;
        public DateTime LastModifiedUtc { get; set; }   // For conflict resolution
        public int? SmartWandId { get; set; }
        public string GPScoordinates { get; set; }
        public bool IsSynced { get; set; } = false;
        public Guid UniqueRecordId { get; set; }
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
    }
}

