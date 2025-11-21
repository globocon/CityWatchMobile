using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class TblClientSiteSmartWandTagsLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GPScoordinates",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientSiteSmartWandTagsLocal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    UId = table.Column<string>(type: "TEXT", nullable: true),
                    TagsTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelDescription = table.Column<string>(type: "TEXT", nullable: true),
                    FqBypass = table.Column<bool>(type: "INTEGER", nullable: false),
                    TagsType = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSiteSmartWandTagsLocal", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientSiteSmartWandTagsLocal");

            migrationBuilder.DropColumn(
                name: "GPScoordinates",
                table: "ClientSiteSmartWandTagsHitLogCache");
        }
    }
}
