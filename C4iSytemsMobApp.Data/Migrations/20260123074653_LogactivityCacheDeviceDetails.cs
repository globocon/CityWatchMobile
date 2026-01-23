using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class LogactivityCacheDeviceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "PostActivityRequestLocalCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "PostActivityRequestLocalCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceId",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "PostActivityRequestLocalCache");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "ClientSiteSmartWandTagsHitLogCache");
        }
    }
}
