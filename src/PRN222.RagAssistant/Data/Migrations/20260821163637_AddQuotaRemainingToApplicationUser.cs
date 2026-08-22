using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.RagAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotaRemainingToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuotaRemaining",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotaRemaining",
                table: "AspNetUsers");
        }
    }
}
