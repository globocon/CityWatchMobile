using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Logactivitycache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Label = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfflineFilesRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordLabel = table.Column<string>(type: "TEXT", nullable: true),
                    FileNameActual = table.Column<string>(type: "TEXT", nullable: true),
                    FileNameCache = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineFilesRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostActivityRequestLocalCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    guardId = table.Column<int>(type: "INTEGER", nullable: false),
                    clientsiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    activityString = table.Column<string>(type: "TEXT", nullable: true),
                    gps = table.Column<string>(type: "TEXT", nullable: true),
                    systemEntry = table.Column<bool>(type: "INTEGER", nullable: false),
                    scanningType = table.Column<int>(type: "INTEGER", nullable: false),
                    tagUID = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsNewGuard = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostActivityRequestLocalCache", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityModel");

            migrationBuilder.DropTable(
                name: "OfflineFilesRecords");

            migrationBuilder.DropTable(
                name: "PostActivityRequestLocalCache");
        }
    }
}
