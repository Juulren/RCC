using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rcc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hotels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DescriptionEs = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hotels", x => x.Id);
                    table.CheckConstraint("CK_Hotels_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Hotels_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_Slug",
                table: "Hotels",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hotels");
        }
    }
}
