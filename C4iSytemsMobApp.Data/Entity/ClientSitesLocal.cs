using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace C4iSytemsMobApp.Data.Entity
{
    public class ClientSitesLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int TypeId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
        public string Gps { get; set; }
        public string Billing { get; set; }
        public int Status { get; set; }
        public DateTime? StatusDate { get; set; }
        public string SiteEmail { get; set; }
        public string LandLine { get; set; }
        public string DuressEmail { get; set; }
        public string DuressSms { get; set; }
        public bool UploadGuardLog { get; set; }
        public bool UploadFusionLog { get; set; }
        public string GuardLogEmailTo { get; set; }
        public bool DataCollectionEnabled { get; set; }
        public bool IsActive { get; set; }
        public bool IsDosDontList { get; set; }
        public bool MobAppShowClientTypeandSite { get; set; }
        public ClientSiteTypeLocal ClientSiteType { get; set; }
    }

    public class ClientSiteTypeLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string TypeName { get; set; }
        public List<ClientSitesLocal> ClientSites { get; set; }
    }

    public class ClientSiteAreaLocal
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public int ClientSiteId { get; set; }
        public string AreaDetails { get; set; }
    }
}
