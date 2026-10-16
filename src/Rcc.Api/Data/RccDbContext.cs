using Microsoft.EntityFrameworkCore;

namespace Rcc.Api.Data;

public sealed class RccDbContext(DbContextOptions<RccDbContext> options) : DbContext(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var hotel = modelBuilder.Entity<Hotel>();
        hotel.ToTable("Hotels", table =>
        {
            table.HasCheckConstraint("CK_Hotels_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            table.HasCheckConstraint("CK_Hotels_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
        });
        hotel.HasKey(x => x.Id);
        hotel.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        hotel.HasIndex(x => x.Slug).IsUnique();
        hotel.Property(x => x.Name).HasMaxLength(180).IsRequired();
        hotel.Property(x => x.DescriptionEs).HasMaxLength(4000).IsRequired();
        hotel.Property(x => x.DescriptionEn).HasMaxLength(4000).IsRequired();
        hotel.Property(x => x.Address).HasMaxLength(500).IsRequired();
        hotel.Property(x => x.Latitude).HasPrecision(9, 6);
        hotel.Property(x => x.Longitude).HasPrecision(9, 6);
        hotel.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
    }
}
