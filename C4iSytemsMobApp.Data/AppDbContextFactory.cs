using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace C4iSytemsMobApp.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "dev.db");
            optionsBuilder.UseSqlite($"Filename={dbPath}");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}