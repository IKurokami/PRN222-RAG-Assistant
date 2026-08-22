using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.RagAssistant.Data.Migrations
{
    public partial class AddPaymentOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalTransactionNo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BankCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CardType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalResponseCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_ExternalOrderId",
                table: "PaymentOrders",
                column: "ExternalOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId_Status",
                table: "PaymentOrders",
                columns: new[] { "UserId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaymentOrders");
        }
    }
}
