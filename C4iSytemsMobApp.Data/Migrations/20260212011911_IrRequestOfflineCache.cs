using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class IrRequestOfflineCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "irOfflineCache",
                columns: table => new
                {
                    IrId = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentRequest = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    guardId = table.Column<int>(type: "INTEGER", nullable: false),
                    clientsiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    gps = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_irOfflineCache", x => x.IrId);
                });

            migrationBuilder.CreateTable(
                name: "irOfflineFilesAttachmentsCache",
                columns: table => new
                {
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IrId = table.Column<string>(type: "TEXT", nullable: true),
                    FileNameActual = table.Column<string>(type: "TEXT", nullable: true),
                    FileNameCache = table.Column<string>(type: "TEXT", nullable: true),
                    FileNameWithPathCache = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    guardId = table.Column<int>(type: "INTEGER", nullable: false),
                    clientsiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    userId = table.Column<int>(type: "INTEGER", nullable: false),
                    gps = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_irOfflineFilesAttachmentsCache", x => x.UniqueRecordId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "irOfflineCache");

            migrationBuilder.DropTable(
                name: "irOfflineFilesAttachmentsCache");
        }
    }
}
