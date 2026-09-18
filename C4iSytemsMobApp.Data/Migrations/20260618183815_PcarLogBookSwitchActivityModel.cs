using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class PcarLogBookSwitchActivityModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityModel",
                table: "ActivityModel");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityModel",
                table: "ActivityModel",
                columns: new[] { "Id", "ClienSiteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityModel",
                table: "ActivityModel");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityModel",
                table: "ActivityModel",
                column: "Id");
        }
    }
}
