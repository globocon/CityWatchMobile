using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalDateTimeAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EventDateTimeLocal",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EventDateTimeLocalWithOffset",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventDateTimeUtcOffsetMinute",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventDateTimeZone",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventDateTimeZoneShort",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventDateTimeLocal",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "EventDateTimeLocalWithOffset",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "EventDateTimeUtcOffsetMinute",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "EventDateTimeZone",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "EventDateTimeZoneShort",
                table: "ClientSiteSmartWandTagsHitLogCache");
        }
    }
}
