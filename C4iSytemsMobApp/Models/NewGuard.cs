using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Models
{
    public class NewGuard
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SecurityNo { get; set; }
        public string Initial { get; set; }
        public string Gender { get; set; }
        public string State { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public bool IsLB_KV_IR { get; set; }
        public bool IsMobileAppAccess { get; set; }
    }
}
