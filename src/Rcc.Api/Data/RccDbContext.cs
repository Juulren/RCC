using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Rcc.Api.Data;

public sealed class RccDbContext(DbContextOptions<RccDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<PhysicalRoom> Rooms => Set<PhysicalRoom>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<RefundJob> RefundJobs => Set<RefundJob>();

    public DbSet<Rcc.Api.Services.Notification> Notifications => Set<Rcc.Api.Services.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IdentityUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "rcc-customer", Name = "Customer", NormalizedName = "CUSTOMER", ConcurrencyStamp = "rcc-customer-v1" },
            new IdentityRole { Id = "rcc-admin", Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "rcc-admin-v1" });
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
        modelBuilder.ConfigureCatalog();
        modelBuilder.ConfigureReservations();
        modelBuilder.ConfigurePayments();
        var notification = modelBuilder.Entity<Rcc.Api.Services.Notification>();
        notification.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_State", "\"State\" IN ('Pending','Accepted','Review')");
            table.HasCheckConstraint("CK_Notifications_Kind", "\"Kind\" IN ('Confirmed','Cancelled')");
        });
        notification.HasKey(x => x.Id);
        notification.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        notification.HasIndex(x => new { x.ReservationId, x.Kind }).IsUnique();
        notification.HasIndex(x => new { x.State, x.NextAttemptAt });
        notification.Property(x => x.Recipient).HasMaxLength(320);
        notification.Property(x => x.Subject).HasMaxLength(200);
        notification.Property(x => x.Sender).HasMaxLength(500);
        notification.Property(x => x.Kind).HasMaxLength(20);
        notification.Property(x => x.State).HasMaxLength(20);
        notification.Property(x => x.LastError).HasMaxLength(100);
        notification.Property(x => x.ProviderId).HasMaxLength(100);
    }
}
