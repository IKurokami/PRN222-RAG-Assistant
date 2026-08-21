using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.RagAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectIdToChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "ChatSessions",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "ChatSessions");
        }
    }
}
