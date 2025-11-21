using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{    
    public class ClientSiteSmartWandTagsLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int ClientSiteId { get; set; }
        public string UId { get; set; }
        public int TagsTypeId { get; set; }
        public string LabelDescription { get; set; }
        public bool FqBypass { get; set; }
        public string TagsType { get; set; }
    }
}
