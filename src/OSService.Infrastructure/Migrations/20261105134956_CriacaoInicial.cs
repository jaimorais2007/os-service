
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OSService.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        private static readonly string[] ServiceOrderStatusIndexColumns =
        {
            "ServiceOrderId",
            "CreatedAt"
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceOrder",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    CustomerId = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    VehicleId = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),

                    Budget = table.Column<decimal>(
                        type: "numeric(18,2)",
                        precision: 18,
                        scale: 2,
                        nullable: false),

                    Inactive = table.Column<bool>(
                        type: "boolean",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ServiceOrder",
                        x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceOrderStatus",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    ServiceOrderId = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    Status = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ServiceOrderStatus",
                        x => x.Id);

                    table.ForeignKey(
                        name: "FK_ServiceOrderStatus_ServiceOrder_ServiceOrderId",
                        column: x => x.ServiceOrderId,
                        principalTable: "ServiceOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrderStatus_ServiceOrderId_CreatedAt",
                table: "ServiceOrderStatus",
                columns: ServiceOrderStatusIndexColumns);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceOrderStatus");

            migrationBuilder.DropTable(
                name: "ServiceOrder");
        }
    }
}
