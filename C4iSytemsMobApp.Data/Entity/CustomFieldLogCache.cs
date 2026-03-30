using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace C4iSytemsMobApp.Data.Entity
{
    [Preserve(AllMembers = true)]
    public class CustomFieldLogHeadCache
    {
        [Key]
        public int Id { get; set; }
        public int SiteId { get; set; }
        public List<CustomFieldLogDetailCache> KeyValuePairs { get; set; }
    }

    [Preserve(AllMembers = true)]
    public class CustomFieldLogDetailCache
    {
        [Key]
        public int Id { get; set; }
        public int HeadId { get; set; }   // FK
        public string DictKey { get; set; }
        public string DictValue { get; set; }
        public CustomFieldLogHeadCache Head { get; set; }
    }


}
