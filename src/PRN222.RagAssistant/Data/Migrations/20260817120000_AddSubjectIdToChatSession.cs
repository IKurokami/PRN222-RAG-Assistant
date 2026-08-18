using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                nullable: false,
                defaultValue: new Guid("d50db4a1-a8b1-4f49-9c43-89d78be62511"));

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_SubjectId",
                table: "ChatSessions",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Subjects_SubjectId",
                table: "ChatSessions",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Subjects_SubjectId",
                table: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_SubjectId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "ChatSessions");
        }
    }
}