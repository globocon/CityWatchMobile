using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class PcarLogBookSwitch2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CallSignId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEntryByPCAR",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LogbookclientsiteId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallSignId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "IsEntryByPCAR",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "LogbookclientsiteId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "OfflineFilesRecords");
        }
    }
}
