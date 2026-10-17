using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Balsm.CareDirectory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCareDirectorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "care_place",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_ar = table.Column<string>(type: "text", nullable: false),
                    address_en = table.Column<string>(type: "text", nullable: false),
                    address_ar = table.Column<string>(type: "text", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    hours = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    rating = table.Column<double>(type: "double precision", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_care_place", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "care_place",
                columns: new[] { "id", "address_ar", "address_en", "country_code", "created_at", "hours", "lat", "lng", "name_ar", "name_en", "phone", "rating", "type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "شارع قصر العيني، القاهرة", "Al-Kasr Al-Aini St, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24/7", 29.976500000000001, 31.235700000000001, "مستشفى قصر العيني", "Qasr Al-Aini Hospital", "+20 2 2365 1234", 4.2000000000000002, "hospital" },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "الزمالك، القاهرة", "Zamalek, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "9am – 5pm", 30.061399999999999, 31.2197, "عيادة د. سارة كمال", "Dr. Sara Kamal Clinic", "+20 10 9876 5432", 4.9000000000000004, "clinic" },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "ميدان التحرير، القاهرة", "Tahrir Sq, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "8am – 12am", 30.0444, 31.235700000000001, "صيدلية العزبي", "El-Ezaby Pharmacy", "+20 2 2574 3210", 4.5, "pharmacy" },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "جاردن سيتي، القاهرة", "Garden City, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "7am – 9pm", 30.034500000000001, 31.231000000000002, "مختبر ألفا سكان", "Alfa Scan Lab", "+20 2 2795 6789", 4.7000000000000002, "lab" },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "المهندسين، الجيزة", "Mohandiseen, Giza", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "8am – 10pm", 30.056100000000001, 31.200299999999999, "مركز القاهرة للأشعة", "Cairo Radiology Center", "+20 2 3304 5678", 4.5999999999999996, "scan" },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "الدقي، الجيزة", "Dokki, Giza", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "9am – 8pm", 30.038399999999999, 31.2119, "الحياة للمستلزمات الطبية", "Al-Hayat Medical Supplies", "+20 2 3761 2345", 4.2999999999999998, "store" },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "العجوزة، الجيزة", "Agouza, Giza", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24/7", 30.056999999999999, 31.210999999999999, "صيدلية سيف", "Seif Pharmacy", "+20 2 3748 9012", 4.4000000000000004, "pharmacy" },
                    { new Guid("00000000-0000-0000-0001-000000000008"), "مدينة نصر، القاهرة", "Nasr City, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "10am – 6pm", 30.051100000000002, 31.365600000000001, "عيادة كابيتال", "Capital Clinic", "+20 2 2402 3456", 4.0999999999999996, "clinic" },
                    { new Guid("00000000-0000-0000-0001-000000000009"), "عين شمس، القاهرة", "Ain Shams, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "24/7", 30.129999999999999, 31.283000000000001, "مستشفى عين شمس التخصصي", "Ain Shams Specialized Hospital", "+20 2 2601 7890", 4.2999999999999998, "hospital" },
                    { new Guid("00000000-0000-0000-0001-000000000010"), "مصر الجديدة، القاهرة", "Heliopolis, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "7am – 11pm", 30.088000000000001, 31.321999999999999, "كايرو لاب", "Cairo Lab", "+20 2 2690 1234", 4.7999999999999998, "lab" },
                    { new Guid("00000000-0000-0000-0001-000000000011"), "المهندسين، الجيزة", "Mohandeseen, Giza", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "8am – 8pm", 30.055499999999999, 31.200500000000002, "مركز الهلال الأخضر للأشعة", "Green Crescent Scan Center", "+20 2 3303 1234", 4.5, "scan" },
                    { new Guid("00000000-0000-0000-0001-000000000012"), "الزمالك، القاهرة", "Zamalek, Cairo", "EG", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "9am – 7pm", 30.062000000000001, 31.219999999999999, "ميدلاين للمستلزمات الطبية", "MedLine Medical Supplies", "+20 2 2736 5678", 4.2000000000000002, "store" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_care_place_country_code_type",
                schema: "public",
                table: "care_place",
                columns: new[] { "country_code", "type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_place",
                schema: "public");
        }
    }
}
