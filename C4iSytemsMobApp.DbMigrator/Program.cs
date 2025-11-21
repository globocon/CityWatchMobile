using System;
using Microsoft.EntityFrameworkCore;
using C4iSytemsMobApp.Data;

namespace DbMigrator
{
    class Program
    {
        static void Main(string[] args)
        {
            using var db = new AppDbContextFactory().CreateDbContext(args);
            db.Database.Migrate();
            Console.WriteLine("Database migrated successfully.");
        }
    }
}