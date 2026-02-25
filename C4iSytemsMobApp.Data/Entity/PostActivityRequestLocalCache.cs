
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{
    public class PostActivityRequestLocalCache
    {
        [Key]
        public int Id { get; set; }
        public int guardId { get; set; }
        public int clientsiteId { get; set; }
        public int userId { get; set; }
        public string activityString { get; set; }
        public string gps { get; set; }
        public bool systemEntry { get; set; } = false;
        public int scanningType { get; set; } = 0;
        public string tagUID { get; set; } = "NA";
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public bool IsNewGuard { get; set; } = false;
        public bool IsSynced { get; set; } = false;
        public Guid UniqueRecordId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }

    }
}
