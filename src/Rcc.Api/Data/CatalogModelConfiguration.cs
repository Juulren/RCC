using Microsoft.EntityFrameworkCore;

namespace Rcc.Api.Data;

public static class CatalogModelConfiguration
{
    public static void ConfigureCatalog(this ModelBuilder model)
    {
        model.Entity<Hotel>().Property(x => x.Revision).IsConcurrencyToken();
        var service = model.Entity<HotelService>();
        service.HasOne<Hotel>().WithMany(x => x.Services).HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        service.Property(x => x.NameEs).HasMaxLength(100).IsRequired();
        service.Property(x => x.NameEn).HasMaxLength(100).IsRequired();
        service.ToTable("HotelServices", table => table.HasCheckConstraint("CK_HotelServices_Order", "\"SortOrder\" BETWEEN 0 AND 19"));
        var type = model.Entity<RoomType>();
        type.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        type.HasAlternateKey(x => new { x.Id, x.HotelId });
        type.HasIndex(x => new { x.HotelId, x.Slug }).IsUnique();
        type.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        type.Property(x => x.NameEs).HasMaxLength(180).IsRequired();
        type.Property(x => x.NameEn).HasMaxLength(180).IsRequired();
        type.Property(x => x.DescriptionEs).HasMaxLength(4000).IsRequired();
        type.Property(x => x.DescriptionEn).HasMaxLength(4000).IsRequired();
        type.Property(x => x.Category).HasMaxLength(10).IsRequired();
        type.Property(x => x.Beds).HasMaxLength(10).IsRequired();
        type.Property(x => x.NightlyRate).HasPrecision(12, 2);
        type.Property(x => x.Revision).IsConcurrencyToken();
        type.ToTable("RoomTypes", table =>
        {
            table.HasCheckConstraint("CK_RoomTypes_Capacity", "\"Capacity\" BETWEEN 1 AND 12 AND \"MaxAdults\" BETWEEN 1 AND \"Capacity\" AND \"MaxChildren\" BETWEEN 0 AND (\"Capacity\" - 1)");
            table.HasCheckConstraint("CK_RoomTypes_Rate", "\"NightlyRate\" > 0 AND \"NightlyRate\" <= 9999999.99");
            table.HasCheckConstraint("CK_RoomTypes_Display", "\"Area\" BETWEEN 1 AND 1000 AND \"SortOrder\" BETWEEN 0 AND 9999 AND \"Category\" IN ('room','suite') AND \"Beds\" IN ('king','choice')");
        });
        var image = model.Entity<RoomImage>();
        image.HasOne<RoomType>().WithMany(x => x.Images).HasForeignKey(x => x.RoomTypeId).OnDelete(DeleteBehavior.Restrict);
        image.Property(x => x.Url).HasMaxLength(500).IsRequired();
        image.Property(x => x.AltEs).HasMaxLength(250).IsRequired();
        image.Property(x => x.AltEn).HasMaxLength(250).IsRequired();
        image.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        image.Property(x => x.Credit).HasMaxLength(250).IsRequired();
        image.HasIndex(x => new { x.RoomTypeId, x.SortOrder }).IsUnique();
        image.ToTable("RoomImages", table => table.HasCheckConstraint("CK_RoomImages_Order", "\"SortOrder\" BETWEEN 0 AND 11"));
        var room = model.Entity<PhysicalRoom>();
        room.HasOne<Hotel>().WithMany().HasForeignKey(x => x.HotelId).OnDelete(DeleteBehavior.Restrict);
        room.HasOne(x => x.RoomType).WithMany(x => x.Rooms).HasForeignKey(x => new { x.RoomTypeId, x.HotelId }).HasPrincipalKey(x => new { x.Id, x.HotelId }).OnDelete(DeleteBehavior.Restrict);
        room.Property(x => x.Number).HasMaxLength(12).IsRequired();
        room.Property(x => x.Revision).IsConcurrencyToken();
        room.HasIndex(x => new { x.HotelId, x.Number }).IsUnique();
        room.ToTable("Rooms", table => table.HasCheckConstraint("CK_Rooms_Number", "\"Number\" ~ '^[A-Z0-9-]{1,12}$'"));
        CatalogSeed.Configure(model);
    }
}
