using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Rcc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Hotels",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "HotelServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameEs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelServices", x => x.Id);
                    table.CheckConstraint("CK_HotelServices_Order", "\"SortOrder\" BETWEEN 0 AND 19");
                    table.ForeignKey(
                        name: "FK_HotelServices_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NameEs = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DescriptionEs = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Beds = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Area = table.Column<int>(type: "integer", nullable: false),
                    RiverView = table.Column<bool>(type: "boolean", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    MaxAdults = table.Column<int>(type: "integer", nullable: false),
                    MaxChildren = table.Column<int>(type: "integer", nullable: false),
                    NightlyRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.Id);
                    table.UniqueConstraint("AK_RoomTypes_Id_HotelId", x => new { x.Id, x.HotelId });
                    table.CheckConstraint("CK_RoomTypes_Capacity", "\"Capacity\" BETWEEN 1 AND 12 AND \"MaxAdults\" BETWEEN 1 AND \"Capacity\" AND \"MaxChildren\" BETWEEN 0 AND (\"Capacity\" - 1)");
                    table.CheckConstraint("CK_RoomTypes_Display", "\"Area\" BETWEEN 1 AND 1000 AND \"SortOrder\" BETWEEN 0 AND 9999 AND \"Category\" IN ('room','suite') AND \"Beds\" IN ('king','choice')");
                    table.CheckConstraint("CK_RoomTypes_Rate", "\"NightlyRate\" > 0 AND \"NightlyRate\" <= 9999999.99");
                    table.ForeignKey(
                        name: "FK_RoomTypes_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AltEs = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    AltEn = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Credit = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomImages", x => x.Id);
                    table.CheckConstraint("CK_RoomImages_Order", "\"SortOrder\" BETWEEN 0 AND 11");
                    table.ForeignKey(
                        name: "FK_RoomImages_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Number", "\"Number\" ~ '^[A-Z0-9-]{1,12}$'");
                    table.ForeignKey(
                        name: "FK_Rooms_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rooms_RoomTypes_RoomTypeId_HotelId",
                        columns: x => new { x.RoomTypeId, x.HotelId },
                        principalTable: "RoomTypes",
                        principalColumns: new[] { "Id", "HotelId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Hotels",
                columns: new[] { "Id", "Address", "DescriptionEn", "DescriptionEs", "Latitude", "Longitude", "Name", "Revision", "Slug", "TimeZone" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), "66 Jinshamen Road, Liangjiang New District, Chongqing, China", "An academic project inspired by Regent Chongqing. Explore our demonstration collection.", "Proyecto académico inspirado en Regent Chongqing. Explora nuestra colección de demostración.", 29.57m, 106.57m, "RCC · Regent Chongqing", 1L, "rcc-chongqing", "Asia/Shanghai" });

            migrationBuilder.InsertData(
                table: "HotelServices",
                columns: new[] { "Id", "HotelId", "NameEn", "NameEs", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("44000000-0000-0000-0000-000000000001"), new Guid("40000000-0000-0000-0000-000000000001"), "Dining · hotel reference", "Gastronomía · referencia del hotel", 0 },
                    { new Guid("44000000-0000-0000-0000-000000000002"), new Guid("40000000-0000-0000-0000-000000000001"), "Wellness · hotel reference", "Bienestar · referencia del hotel", 1 }
                });

            migrationBuilder.InsertData(
                table: "RoomTypes",
                columns: new[] { "Id", "Active", "Area", "Beds", "Capacity", "Category", "DescriptionEn", "DescriptionEs", "HotelId", "MaxAdults", "MaxChildren", "NameEn", "NameEs", "NightlyRate", "Revision", "RiverView", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("41000000-0000-0000-0000-000000000001"), true, 57, "choice", 3, "room", "Warm materials and Chinese-inspired details for a retreat above the city.", "Materiales cálidos y detalles de inspiración china para descansar sobre la ciudad.", new Guid("40000000-0000-0000-0000-000000000001"), 2, 2, "Classic Room", "Classic Room", 880m, 1L, false, "classic", 1 },
                    { new Guid("41000000-0000-0000-0000-000000000002"), true, 57, "choice", 3, "room", "A perspective on the Jialing River, with warm tones and serene interiors.", "Una perspectiva del río Jialing, con tonos cálidos y un interior sereno.", new Guid("40000000-0000-0000-0000-000000000001"), 2, 2, "Classic Room River View", "Classic Room River View", 1080m, 1L, true, "classic-river", 2 },
                    { new Guid("41000000-0000-0000-0000-000000000003"), true, 60, "king", 3, "room", "Room to slow down, with city views and Regent Club access as described by the hotel.", "Espacio para bajar el ritmo, con vistas urbanas y acceso al Regent Club según la oferta oficial.", new Guid("40000000-0000-0000-0000-000000000001"), 2, 2, "Regent Club Room", "Regent Club Room", 1280m, 1L, false, "club", 3 },
                    { new Guid("41000000-0000-0000-0000-000000000004"), true, 60, "choice", 3, "room", "Natural light and river views from a contemporary, locally inspired space.", "Luz natural y vistas al río desde un espacio contemporáneo de inspiración local.", new Guid("40000000-0000-0000-0000-000000000001"), 2, 2, "Regent Club Room River View", "Regent Club Room River View", 1480m, 1L, true, "club-river", 4 },
                    { new Guid("41000000-0000-0000-0000-000000000005"), true, 83, "king", 4, "suite", "A residential retreat where city, sky and river come together.", "Un refugio residencial donde se encuentran ciudad, cielo y río.", new Guid("40000000-0000-0000-0000-000000000001"), 3, 2, "Junior Suite", "Junior Suite", 1880m, 1L, true, "junior", 5 },
                    { new Guid("41000000-0000-0000-0000-000000000006"), true, 117, "king", 4, "suite", "A spacious suite with crafted details and distinct spaces to unwind.", "Una suite amplia con detalles artesanales y espacios diferenciados para descansar.", new Guid("40000000-0000-0000-0000-000000000001"), 3, 2, "Executive Suite", "Executive Suite", 2680m, 1L, false, "executive", 6 },
                    { new Guid("41000000-0000-0000-0000-000000000007"), true, 215, "king", 4, "suite", "The most expansive option in this selection: residential spaces on the top floor.", "La propuesta más amplia de esta selección: espacios residenciales en la planta superior.", new Guid("40000000-0000-0000-0000-000000000001"), 3, 2, "Regent Suite", "Regent Suite", 4880m, 1L, false, "regent", 7 }
                });

            migrationBuilder.InsertData(
                table: "RoomImages",
                columns: new[] { "Id", "AltEn", "AltEs", "Credit", "RoomTypeId", "SortOrder", "SourceUrl", "Url" },
                values: new object[,]
                {
                    { new Guid("42000000-0000-0000-0000-000000000010"), "Classic Room · photograph 1", "Classic Room · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000001"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/classic.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000011"), "Classic Room · photograph 2", "Classic Room · fotografía 2", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000001"), 1, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/classic-bathroom.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000020"), "Classic Room River View · photograph 1", "Classic Room River View · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000002"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/classic-river.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000030"), "Regent Club Room · photograph 1", "Regent Club Room · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000003"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/club.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000040"), "Regent Club Room River View · photograph 1", "Regent Club Room River View · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000004"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/club-river.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000050"), "Junior Suite · photograph 1", "Junior Suite · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000005"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/junior-living.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000051"), "Junior Suite · photograph 2", "Junior Suite · fotografía 2", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000005"), 1, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/junior.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000060"), "Executive Suite · photograph 1", "Executive Suite · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000006"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/executive.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000061"), "Executive Suite · photograph 2", "Executive Suite · fotografía 2", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000006"), 1, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/executive-living.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000070"), "Regent Suite · photograph 1", "Regent Suite · fotografía 1", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000007"), 0, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/regent.webp" },
                    { new Guid("42000000-0000-0000-0000-000000000071"), "Regent Suite · photograph 2", "Regent Suite · fotografía 2", "Regent / IHG · Copyright de sus titulares", new Guid("41000000-0000-0000-0000-000000000007"), 1, "https://www.ihg.com/regent/hotels/us/en/chongqing/ckgrh/hoteldetail/rooms-and-suites", "/images/regent/regent-living.webp" }
                });

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Active", "HotelId", "Number", "Revision", "RoomTypeId" },
                values: new object[,]
                {
                    { new Guid("43000000-0000-0000-0000-000000000011"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-101", 1L, new Guid("41000000-0000-0000-0000-000000000001") },
                    { new Guid("43000000-0000-0000-0000-000000000012"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-102", 1L, new Guid("41000000-0000-0000-0000-000000000001") },
                    { new Guid("43000000-0000-0000-0000-000000000021"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-201", 1L, new Guid("41000000-0000-0000-0000-000000000002") },
                    { new Guid("43000000-0000-0000-0000-000000000022"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-202", 1L, new Guid("41000000-0000-0000-0000-000000000002") },
                    { new Guid("43000000-0000-0000-0000-000000000031"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-301", 1L, new Guid("41000000-0000-0000-0000-000000000003") },
                    { new Guid("43000000-0000-0000-0000-000000000032"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-302", 1L, new Guid("41000000-0000-0000-0000-000000000003") },
                    { new Guid("43000000-0000-0000-0000-000000000041"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-401", 1L, new Guid("41000000-0000-0000-0000-000000000004") },
                    { new Guid("43000000-0000-0000-0000-000000000042"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-402", 1L, new Guid("41000000-0000-0000-0000-000000000004") },
                    { new Guid("43000000-0000-0000-0000-000000000051"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-501", 1L, new Guid("41000000-0000-0000-0000-000000000005") },
                    { new Guid("43000000-0000-0000-0000-000000000052"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-502", 1L, new Guid("41000000-0000-0000-0000-000000000005") },
                    { new Guid("43000000-0000-0000-0000-000000000061"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-601", 1L, new Guid("41000000-0000-0000-0000-000000000006") },
                    { new Guid("43000000-0000-0000-0000-000000000062"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-602", 1L, new Guid("41000000-0000-0000-0000-000000000006") },
                    { new Guid("43000000-0000-0000-0000-000000000071"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-701", 1L, new Guid("41000000-0000-0000-0000-000000000007") },
                    { new Guid("43000000-0000-0000-0000-000000000072"), true, new Guid("40000000-0000-0000-0000-000000000001"), "DEMO-702", 1L, new Guid("41000000-0000-0000-0000-000000000007") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelServices_HotelId",
                table: "HotelServices",
                column: "HotelId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomImages_RoomTypeId_SortOrder",
                table: "RoomImages",
                columns: new[] { "RoomTypeId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_Number",
                table: "Rooms",
                columns: new[] { "HotelId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomTypeId_HotelId",
                table: "Rooms",
                columns: new[] { "RoomTypeId", "HotelId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_HotelId_Slug",
                table: "RoomTypes",
                columns: new[] { "HotelId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelServices");

            migrationBuilder.DropTable(
                name: "RoomImages");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "RoomTypes");

            migrationBuilder.DeleteData(
                table: "Hotels",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Hotels");
        }
    }
}
