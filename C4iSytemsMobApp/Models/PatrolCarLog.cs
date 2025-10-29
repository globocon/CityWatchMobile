
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Models
{
    public class PatrolCarLog
    {
        public int Id { get; set; }
        public int PatrolCarId { get; set; }
        public int ClientSiteLogBookId { get; set; }
        public decimal Mileage { get; set; }
        public string MileageText { get; set; }
        public string PatrolCar { get; set; }
        public ClientSitePatrolCar ClientSitePatrolCar { get; set; }
    }
}
