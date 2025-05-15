using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Models
{
    public class ClientSiteMobileAppSettings
    {
        public int Id { get; set; }
        public int ClientSiteId { get; set; }
        public bool IsCrowdCountEnabled { get; set; }
        public bool IsDoorEnabled { get; set; }
        public bool IsGateEnabled { get; set; }
        public bool IsLevelFloorEnabled { get; set; }
        public bool IsRoomEnabled { get; set; }
        public int CounterQuantity { get; set; }
    }
}
