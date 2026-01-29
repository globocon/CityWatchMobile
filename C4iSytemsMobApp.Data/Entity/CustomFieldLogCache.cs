using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace C4iSytemsMobApp.Data.Entity
{
    public class CustomFieldLogHeadCache
    {
        [Key]
        public int Id { get; set; }
        public int SiteId { get; set; }
        public List<CustomFieldLogDetailCache> KeyValuePairs { get; set; }
    }

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
