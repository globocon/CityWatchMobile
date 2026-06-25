using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class PcarLogBookSwitch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CallSignId",
                table: "PostActivityRequestLocalCache",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEntryByPCAR",
                table: "PostActivityRequestLocalCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LogbookclientsiteId",
                table: "PostActivityRequestLocalCache",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId",
                table: "PostActivityRequestLocalCache",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClienSiteId",
                table: "ActivityModel",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallSignId",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "IsEntryByPCAR",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "LogbookclientsiteId",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "ClienSiteId",
                table: "ActivityModel");
        }
    }
}
