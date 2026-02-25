using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class patrolcarandcustomlogoffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientSitePatrolCarCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    Rego = table.Column<string>(type: "TEXT", nullable: true),
                    ClientSiteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSitePatrolCarCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldLogHeadCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldLogHeadCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldLogRequestHeadCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldLogRequestHeadCache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatrolCarLogRequestCache",
                columns: table => new
                {
                    CacheId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientSiteLogBookId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mileage = table.Column<decimal>(type: "TEXT", nullable: false),
                    MileageText = table.Column<string>(type: "TEXT", nullable: true),
                    PatrolCar = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeLocal = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EventDateTimeLocalWithOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EventDateTimeZone = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeZoneShort = table.Column<string>(type: "TEXT", nullable: true),
                    EventDateTimeUtcOffsetMinute = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    UniqueRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true),
                    PatrolCarId = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    Rego = table.Column<string>(type: "TEXT", nullable: true),
                    ClientSiteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrolCarLogRequestCache", x => x.CacheId);
                });

            migrationBuilder.CreateTable(
                name: "PatrolCarLogCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    PatrolCarId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientSiteLogBookId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mileage = table.Column<decimal>(type: "TEXT", nullable: false),
                    MileageText = table.Column<string>(type: "TEXT", nullable: true),
                    PatrolCar = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatrolCarLogCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatrolCarLogCache_ClientSitePatrolCarCache_PatrolCarId",
                        column: x => x.PatrolCarId,
                        principalTable: "ClientSitePatrolCarCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldLogDetailCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeadId = table.Column<int>(type: "INTEGER", nullable: false),
                    DictKey = table.Column<string>(type: "TEXT", nullable: true),
                    DictValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldLogDetailCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldLogDetailCache_CustomFieldLogHeadCache_HeadId",
                        column: x => x.HeadId,
                        principalTable: "CustomFieldLogHeadCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldLogRequestDetailCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeadId = table.Column<int>(type: "INTEGER", nullable: false),
                    DictKey = table.Column<string>(type: "TEXT", nullable: true),
                    DictValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldLogRequestDetailCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldLogRequestDetailCache_CustomFieldLogRequestHeadCache_HeadId",
                        column: x => x.HeadId,
                        principalTable: "CustomFieldLogRequestHeadCache",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldLogDetailCache_HeadId",
                table: "CustomFieldLogDetailCache",
                column: "HeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldLogRequestDetailCache_HeadId",
                table: "CustomFieldLogRequestDetailCache",
                column: "HeadId");

            migrationBuilder.CreateIndex(
                name: "IX_PatrolCarLogCache_PatrolCarId",
                table: "PatrolCarLogCache",
                column: "PatrolCarId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldLogDetailCache");

            migrationBuilder.DropTable(
                name: "CustomFieldLogRequestDetailCache");

            migrationBuilder.DropTable(
                name: "PatrolCarLogCache");

            migrationBuilder.DropTable(
                name: "PatrolCarLogRequestCache");

            migrationBuilder.DropTable(
                name: "CustomFieldLogHeadCache");

            migrationBuilder.DropTable(
                name: "CustomFieldLogRequestHeadCache");

            migrationBuilder.DropTable(
                name: "ClientSitePatrolCarCache");
        }
    }
}
