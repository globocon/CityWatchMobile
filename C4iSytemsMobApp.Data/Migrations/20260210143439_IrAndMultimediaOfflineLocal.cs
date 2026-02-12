using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class IrAndMultimediaOfflineLocal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioAndMultimediaLocal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AudioType = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    ServerUrl = table.Column<string>(type: "TEXT", nullable: true),
                    LocalFilePath = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAndMultimediaLocal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientSiteAreaLocal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientSiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    AreaDetails = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSiteAreaLocal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientSiteTypeLocal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    TypeName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSiteTypeLocal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IrFeedbackTemplateViewModelLocal",
                columns: table => new
                {
                    TemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateName = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: true),
                    FeedbackTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    BackgroundColour = table.Column<string>(type: "TEXT", nullable: true),
                    TextColor = table.Column<string>(type: "TEXT", nullable: true),
                    DeleteStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SendtoRC = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "IrNotifiedByLocal",
                columns: table => new
                {
                    NotifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "ClientSitesLocal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    TypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: true),
                    Gps = table.Column<string>(type: "TEXT", nullable: true),
                    Billing = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SiteEmail = table.Column<string>(type: "TEXT", nullable: true),
                    LandLine = table.Column<string>(type: "TEXT", nullable: true),
                    DuressEmail = table.Column<string>(type: "TEXT", nullable: true),
                    DuressSms = table.Column<string>(type: "TEXT", nullable: true),
                    UploadGuardLog = table.Column<bool>(type: "INTEGER", nullable: false),
                    UploadFusionLog = table.Column<bool>(type: "INTEGER", nullable: false),
                    GuardLogEmailTo = table.Column<string>(type: "TEXT", nullable: true),
                    DataCollectionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDosDontList = table.Column<bool>(type: "INTEGER", nullable: false),
                    MobAppShowClientTypeandSite = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSitesLocal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientSitesLocal_ClientSiteTypeLocal_TypeId",
                        column: x => x.TypeId,
                        principalTable: "ClientSiteTypeLocal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientSitesLocal_TypeId",
                table: "ClientSitesLocal",
                column: "TypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioAndMultimediaLocal");

            migrationBuilder.DropTable(
                name: "ClientSiteAreaLocal");

            migrationBuilder.DropTable(
                name: "ClientSitesLocal");

            migrationBuilder.DropTable(
                name: "IrFeedbackTemplateViewModelLocal");

            migrationBuilder.DropTable(
                name: "IrNotifiedByLocal");

            migrationBuilder.DropTable(
                name: "ClientSiteTypeLocal");
        }
    }
}
