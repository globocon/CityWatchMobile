using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkedsiteScanChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScanFromLinkedSite",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RCLinkedDuressClientSitesCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    RCLinkedId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientSiteId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCLinkedDuressClientSitesCache", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCLinkedDuressClientSitesCache");

            migrationBuilder.DropColumn(
                name: "IsScanFromLinkedSite",
                table: "ClientSiteSmartWandTagsHitLogCache");
        }
    }
}
