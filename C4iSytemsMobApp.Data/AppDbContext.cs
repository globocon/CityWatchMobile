using C4iSytemsMobApp.Data.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace C4iSytemsMobApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientSiteSmartWandTagsHitLogCache> ClientSiteSmartWandTagsHitLogCache { get; set; }
        public DbSet<ClientSiteSmartWandTagsLocal> ClientSiteSmartWandTagsLocal { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.Migrate();
        }
    }
}