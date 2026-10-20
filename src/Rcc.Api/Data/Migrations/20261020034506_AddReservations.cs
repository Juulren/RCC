using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rcc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Arrival = table.Column<DateOnly>(type: "date", nullable: false),
                    Departure = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservations_Currency", "\"Currency\" = 'CNY'");
                    table.CheckConstraint("CK_Reservations_Dates", "\"Departure\" > \"Arrival\"");
                    table.CheckConstraint("CK_Reservations_Locale", "\"Locale\" IN ('es','en')");
                    table.CheckConstraint("CK_Reservations_Status", "\"Status\" IN ('PendingPayment','Confirmed','Expired','Cancelled')");
                    table.CheckConstraint("CK_Reservations_Total", "\"Total\" > 0");
                    table.ForeignKey(
                        name: "FK_Reservations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdempotencyRequests_Reservations_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameEs = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NightlyRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Adults = table.Column<int>(type: "integer", nullable: false),
                    ChildAges = table.Column<int[]>(type: "integer[]", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationItems", x => x.Id);
                    table.CheckConstraint("CK_ReservationItems_Guests", "\"Adults\" BETWEEN 1 AND 12 AND cardinality(\"ChildAges\") <= 11 AND 0 <= ALL(\"ChildAges\") AND 17 >= ALL(\"ChildAges\")");
                    table.CheckConstraint("CK_ReservationItems_Price", "\"NightlyRate\" > 0 AND \"Subtotal\" > 0");
                    table.ForeignKey(
                        name: "FK_ReservationItems_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationItems_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRequests_ResourceId",
                table: "IdempotencyRequests",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRequests_UserId_Operation_Key",
                table: "IdempotencyRequests",
                columns: new[] { "UserId", "Operation", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationItems_ReservationId_RoomId",
                table: "ReservationItems",
                columns: new[] { "ReservationId", "RoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationItems_RoomId",
                table: "ReservationItems",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status_ExpiresAt",
                table: "Reservations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId_CreatedAt",
                table: "Reservations",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRequests");

            migrationBuilder.DropTable(
                name: "ReservationItems");

            migrationBuilder.DropTable(
                name: "Reservations");
        }
    }
}
