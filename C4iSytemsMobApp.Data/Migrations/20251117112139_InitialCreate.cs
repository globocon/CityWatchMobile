using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientSiteSmartWandTagsHitLogCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoggedInClientSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoggedInUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoggedInGuardId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagUId = table.Column<string>(type: "TEXT", nullable: true),
                    TagsTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    LabelDescription = table.Column<string>(type: "TEXT", nullable: true),
                    TagLinkedClientSiteId = table.Column<int>(type: "INTEGER", nullable: true),
                    HitUtcDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HitLocalDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SmartWandId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSiteSmartWandTagsHitLogCache", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientSiteSmartWandTagsHitLogCache");
        }
    }
}
