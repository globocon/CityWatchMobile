using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Models
{
    public class CustomFieldLogRequestHeadDto
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public List<CustomFieldLogRequestDetailDto> Details { get; set; }
        public DateTime? EventDateTimeLocal { get; set; }
        public DateTimeOffset? EventDateTimeLocalWithOffset { get; set; }
        public string EventDateTimeZone { get; set; }
        public string EventDateTimeZoneShort { get; set; }
        public int? EventDateTimeUtcOffsetMinute { get; set; }
        public bool IsSynced { get; set; } = false;
        public Guid UniqueRecordId { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
    }

    public class CustomFieldLogRequestDetailDto
    {
        public int Id { get; set; }
        public int HeadId { get; set; }
        public string DictKey { get; set; }
        public string DictValue { get; set; }
    }

}
