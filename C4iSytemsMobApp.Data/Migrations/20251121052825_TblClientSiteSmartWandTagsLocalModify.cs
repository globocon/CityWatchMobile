using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class TblClientSiteSmartWandTagsLocalModify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelDescription",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.DropColumn(
                name: "TagLinkedClientSiteId",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.AlterColumn<int>(
                name: "TagsTypeId",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UniqueRecordId",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UniqueRecordId",
                table: "ClientSiteSmartWandTagsHitLogCache");

            migrationBuilder.AlterColumn<int>(
                name: "TagsTypeId",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "LabelDescription",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TagLinkedClientSiteId",
                table: "ClientSiteSmartWandTagsHitLogCache",
                type: "INTEGER",
                nullable: true);
        }
    }
}
