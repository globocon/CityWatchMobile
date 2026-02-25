using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Models
{
    public class irOfflineCacheDto
    {
        public string IrId { get; set; }
        public IncidentRequest IncidentRequest { get; set; }
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public bool IsSynced { get; set; } = false;
        public Guid UniqueRecordId { get; set; } = Guid.NewGuid();
        public int guardId { get; set; }
        public int clientsiteId { get; set; }
        public int userId { get; set; }
        public string gps { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
    }
}
