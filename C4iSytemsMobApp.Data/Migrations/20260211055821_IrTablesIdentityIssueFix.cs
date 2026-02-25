using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace C4iSytemsMobApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class IrTablesIdentityIssueFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "IrNotifiedByLocal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "IrFeedbackTemplateViewModelLocal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_IrNotifiedByLocal",
                table: "IrNotifiedByLocal",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IrFeedbackTemplateViewModelLocal",
                table: "IrFeedbackTemplateViewModelLocal",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_IrNotifiedByLocal",
                table: "IrNotifiedByLocal");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IrFeedbackTemplateViewModelLocal",
                table: "IrFeedbackTemplateViewModelLocal");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "IrNotifiedByLocal");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "IrFeedbackTemplateViewModelLocal");
        }
    }
}
