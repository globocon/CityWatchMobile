using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Logactivitydocumentcache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileGroupId",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FileNameWithPathCache",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNew",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LogBookId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "clientsiteId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "gps",
                table: "OfflineFilesRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "guardId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "userId",
                table: "OfflineFilesRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileGroupId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "FileNameWithPathCache",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "IsNew",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "LogBookId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "clientsiteId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "gps",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "guardId",
                table: "OfflineFilesRecords");

            migrationBuilder.DropColumn(
                name: "userId",
                table: "OfflineFilesRecords");
        }
    }
}
