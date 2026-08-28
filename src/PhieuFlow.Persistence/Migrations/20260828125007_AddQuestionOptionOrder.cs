using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhieuFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionOptionOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "QuestionOptions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Order",
                table: "QuestionOptions");
        }
    }
}
