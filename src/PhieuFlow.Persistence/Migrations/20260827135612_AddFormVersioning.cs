using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhieuFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormPages_Forms_FormId",
                table: "FormPages");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Forms");

            migrationBuilder.RenameColumn(
                name: "FormId",
                table: "FormPages",
                newName: "FormVersionId");

            migrationBuilder.RenameIndex(
                name: "IX_FormPages_FormId",
                table: "FormPages",
                newName: "IX_FormPages_FormVersionId");

            migrationBuilder.CreateTable(
                name: "FormVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormVersions_Forms_FormId",
                        column: x => x.FormId,
                        principalTable: "Forms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormVersions_FormId_VersionNumber",
                table: "FormVersions",
                columns: new[] { "FormId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FormPages_FormVersions_FormVersionId",
                table: "FormPages",
                column: "FormVersionId",
                principalTable: "FormVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormPages_FormVersions_FormVersionId",
                table: "FormPages");

            migrationBuilder.DropTable(
                name: "FormVersions");

            migrationBuilder.RenameColumn(
                name: "FormVersionId",
                table: "FormPages",
                newName: "FormId");

            migrationBuilder.RenameIndex(
                name: "IX_FormPages_FormVersionId",
                table: "FormPages",
                newName: "IX_FormPages_FormId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Forms",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastModifiedAt",
                table: "Forms",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Forms",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Forms",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Forms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_FormPages_Forms_FormId",
                table: "FormPages",
                column: "FormId",
                principalTable: "Forms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
