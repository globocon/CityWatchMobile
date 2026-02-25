
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{
    public class PatrolCarLogCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int PatrolCarId { get; set; }
        public int ClientSiteLogBookId { get; set; }
        public decimal Mileage { get; set; }
        public string MileageText { get; set; }
        public string PatrolCar { get; set; }
        public ClientSitePatrolCarCache ClientSitePatrolCar { get; set; }
    }

    public class ClientSitePatrolCarCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Model { get; set; }
        public string Rego { get; set; }
        public int ClientSiteId { get; set; }
        // One-to-one navigation
        public PatrolCarLogCache PatrolCarLog { get; set; }
    }
}
