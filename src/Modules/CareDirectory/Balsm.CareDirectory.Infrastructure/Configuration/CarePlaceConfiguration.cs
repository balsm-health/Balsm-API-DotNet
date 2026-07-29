using Balsm.CareDirectory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Balsm.CareDirectory.Infrastructure.Configuration;

public sealed class CarePlaceConfiguration : IEntityTypeConfiguration<CarePlace>
{
    // Deterministic seed timestamp so the InsertData migration is reproducible
    // (no DateTime.UtcNow drift between `migrations add` runs).
    private static readonly DateTime SeedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // 12 public NON-PHI care places (Greater Cairo, CountryCode="EG"). Row order
    // here maps to the fixed GUID pattern 00000000-0000-0000-0001-0000000000NN
    // (NN = 1-based position, zero-padded to 2).
    private static readonly (string Type, string NameEn, string NameAr, string AddressEn, string AddressAr,
        double Lat, double Lng, string Hours, string Phone, double Rating)[] Seed =
    [
        ("hospital", "Qasr Al-Aini Hospital", "مستشفى قصر العيني", "Al-Kasr Al-Aini St, Cairo", "شارع قصر العيني، القاهرة", 29.9765, 31.2357, "24/7", "+20 2 2365 1234", 4.2),
        ("clinic", "Dr. Sara Kamal Clinic", "عيادة د. سارة كمال", "Zamalek, Cairo", "الزمالك، القاهرة", 30.0614, 31.2197, "9am – 5pm", "+20 10 9876 5432", 4.9),
        ("pharmacy", "El-Ezaby Pharmacy", "صيدلية العزبي", "Tahrir Sq, Cairo", "ميدان التحرير، القاهرة", 30.0444, 31.2357, "8am – 12am", "+20 2 2574 3210", 4.5),
        ("lab", "Alfa Scan Lab", "مختبر ألفا سكان", "Garden City, Cairo", "جاردن سيتي، القاهرة", 30.0345, 31.2310, "7am – 9pm", "+20 2 2795 6789", 4.7),
        ("scan", "Cairo Radiology Center", "مركز القاهرة للأشعة", "Mohandiseen, Giza", "المهندسين، الجيزة", 30.0561, 31.2003, "8am – 10pm", "+20 2 3304 5678", 4.6),
        ("store", "Al-Hayat Medical Supplies", "الحياة للمستلزمات الطبية", "Dokki, Giza", "الدقي، الجيزة", 30.0384, 31.2119, "9am – 8pm", "+20 2 3761 2345", 4.3),
        ("pharmacy", "Seif Pharmacy", "صيدلية سيف", "Agouza, Giza", "العجوزة، الجيزة", 30.0570, 31.2110, "24/7", "+20 2 3748 9012", 4.4),
        ("clinic", "Capital Clinic", "عيادة كابيتال", "Nasr City, Cairo", "مدينة نصر، القاهرة", 30.0511, 31.3656, "10am – 6pm", "+20 2 2402 3456", 4.1),
        ("hospital", "Ain Shams Specialized Hospital", "مستشفى عين شمس التخصصي", "Ain Shams, Cairo", "عين شمس، القاهرة", 30.1300, 31.2830, "24/7", "+20 2 2601 7890", 4.3),
        ("lab", "Cairo Lab", "كايرو لاب", "Heliopolis, Cairo", "مصر الجديدة، القاهرة", 30.0880, 31.3220, "7am – 11pm", "+20 2 2690 1234", 4.8),
        ("scan", "Green Crescent Scan Center", "مركز الهلال الأخضر للأشعة", "Mohandeseen, Giza", "المهندسين، الجيزة", 30.0555, 31.2005, "8am – 8pm", "+20 2 3303 1234", 4.5),
        ("store", "MedLine Medical Supplies", "ميدلاين للمستلزمات الطبية", "Zamalek, Cairo", "الزمالك، القاهرة", 30.0620, 31.2200, "9am – 7pm", "+20 2 2736 5678", 4.2)
    ];

    public void Configure(EntityTypeBuilder<CarePlace> builder)
    {
        builder.ToTable("care_place");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.NameEn).HasColumnName("name_en").IsRequired();
        builder.Property(x => x.NameAr).HasColumnName("name_ar").IsRequired();
        builder.Property(x => x.AddressEn).HasColumnName("address_en").IsRequired();
        builder.Property(x => x.AddressAr).HasColumnName("address_ar").IsRequired();
        builder.Property(x => x.Lat).HasColumnName("lat");
        builder.Property(x => x.Lng).HasColumnName("lng");
        builder.Property(x => x.Hours).HasColumnName("hours").IsRequired();
        builder.Property(x => x.Phone).HasColumnName("phone").IsRequired();
        builder.Property(x => x.Rating).HasColumnName("rating");
        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.CountryCode, x.Type });

        // HasData bypasses the private ctor / factory, so anonymous objects carry
        // every column (including the deterministic Id and CreatedAt).
        builder.HasData(Seed.Select((r, i) => new
        {
            Id = Guid.Parse($"00000000-0000-0000-0001-0000000000{i + 1:D2}"),
            r.Type,
            r.NameEn,
            r.NameAr,
            r.AddressEn,
            r.AddressAr,
            r.Lat,
            r.Lng,
            r.Hours,
            r.Phone,
            r.Rating,
            CountryCode = "EG",
            CreatedAt = SeedTime
        }));
    }
}
