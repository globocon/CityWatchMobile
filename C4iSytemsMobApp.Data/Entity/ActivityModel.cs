using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{  
    [Preserve(AllMembers = true)]
    public class ActivityModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
    }
}
