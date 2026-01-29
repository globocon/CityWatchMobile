using C4iSytemsMobApp.Data.Entity;
using Microsoft.EntityFrameworkCore;

namespace C4iSytemsMobApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientSiteSmartWandTagsHitLogCache> ClientSiteSmartWandTagsHitLogCache { get; set; }
        public DbSet<ClientSiteSmartWandTagsLocal> ClientSiteSmartWandTagsLocal { get; set; }
        public DbSet<ActivityModel> ActivityModel { get; set; }
        public DbSet<PostActivityRequestLocalCache> PostActivityRequestLocalCache { get; set; }
        public DbSet<OfflineFilesRecords> OfflineFilesRecords { get; set; }
        public DbSet<ClientSitePatrolCarCache> ClientSitePatrolCarCache { get; set; }
        public DbSet<PatrolCarLogCache> PatrolCarLogCache { get; set; }
        public DbSet<PatrolCarLogRequestCache> PatrolCarLogRequestCache { get; set; }
        public DbSet<CustomFieldLogRequestHeadCache> CustomFieldLogRequestHeadCache { get; set; }
        public DbSet<CustomFieldLogRequestDetailCache> CustomFieldLogRequestDetailCache { get; set; }
        public DbSet<CustomFieldLogHeadCache> CustomFieldLogHeadCache { get; set; }
        public DbSet<CustomFieldLogDetailCache> CustomFieldLogDetailCache { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.Migrate();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CustomFieldLogRequestHeadCache>()
                .HasMany(h => h.Details)
                .WithOne(d => d.Head)
                .HasForeignKey(d => d.HeadId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClientSitePatrolCarCache>()
                .HasOne(p => p.PatrolCarLog)
                .WithOne(l => l.ClientSitePatrolCar)
                .HasForeignKey<PatrolCarLogCache>(l => l.PatrolCarId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomFieldLogHeadCache>()
                .HasMany(h => h.KeyValuePairs)
                .WithOne(d => d.Head)
                .HasForeignKey(d => d.HeadId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}