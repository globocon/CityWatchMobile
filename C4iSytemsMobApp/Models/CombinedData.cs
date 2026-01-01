using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C4iSytemsMobApp.Models
{
    public class CombinedData
    {
        public int HRGroupId { get; set; }
        public string Description { get; set; }
        public string UsedDescription { get; set; }
        public string ReferenceNo { get; set; }
        public int ID { get; set; }
        public string DisplayDescription
        {
            get
            {
                if (Description != null)
                {
                    if (UsedDescription == null)
                    {
                        return $"{ReferenceNo} {Description} {"[❌]"}";
                    }
                    else
                    {
                        return $"{ReferenceNo} {Description} {"[✔️]"}";
                    }
                        
                }
                else
                {
                    return $"{ReferenceNo} {Description} {"[❌]"}";
                }
            }
        }

        public string ReferenceNoAndDescription
        {
            get
            {
                return $"{ReferenceNo} {Description}";
            }
        }
    }
}
