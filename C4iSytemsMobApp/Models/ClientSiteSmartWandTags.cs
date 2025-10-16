
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Models
{
    public class ClientSiteSmartWandTags
    {
        public int Id { get; set; }
        public int ClientSiteId { get; set; }
        public string UId { get; set; }
        public int TagsTypeId { get; set; }
        public string LabelDescription { get; set; }
        public bool FqBypass { get; set; }
        public string? TagsType { get; set; }
        public bool IsDeleted { get; set; }
    }
}
