using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Rcc.Api.Data;

public static class ReservationModelConfiguration
{
    public static void ConfigureReservations(this ModelBuilder model)
    {
        var reservation = model.Entity<Reservation>();
        reservation.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_Dates", "\"Departure\" > \"Arrival\"");
            table.HasCheckConstraint("CK_Reservations_Total", "\"Total\" > 0");
            table.HasCheckConstraint("CK_Reservations_Status", "\"Status\" IN ('PendingPayment','Confirmed','Expired','Cancelled')");
            table.HasCheckConstraint("CK_Reservations_Currency", "\"Currency\" = 'CNY'");
            table.HasCheckConstraint("CK_Reservations_Locale", "\"Locale\" IN ('es','en')");
        });
        reservation.HasKey(x => x.Id);
        reservation.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        reservation.Property(x => x.Status).HasMaxLength(20);
        reservation.Property(x => x.Currency).HasMaxLength(3);
        reservation.Property(x => x.Locale).HasMaxLength(2);
        reservation.Property(x => x.Total).HasPrecision(12, 2);
        reservation.HasIndex(x => new { x.UserId, x.CreatedAt });
        reservation.HasIndex(x => new { x.Status, x.ExpiresAt });
        var item = model.Entity<ReservationItem>();
        item.ToTable("ReservationItems", table =>
        {
            table.HasCheckConstraint("CK_ReservationItems_Price", "\"NightlyRate\" > 0 AND \"Subtotal\" > 0");
            table.HasCheckConstraint("CK_ReservationItems_Guests", "\"Adults\" BETWEEN 1 AND 12 AND cardinality(\"ChildAges\") <= 11 AND 0 <= ALL(\"ChildAges\") AND 17 >= ALL(\"ChildAges\")");
        });
        item.HasKey(x => x.Id);
        item.HasOne(x => x.Reservation).WithMany(x => x.Items).HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        item.HasOne<PhysicalRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        item.HasIndex(x => new { x.ReservationId, x.RoomId }).IsUnique();
        item.HasIndex(x => x.RoomId);
        item.Property(x => x.NameEs).HasMaxLength(180);
        item.Property(x => x.NameEn).HasMaxLength(180);
        item.Property(x => x.NightlyRate).HasPrecision(12, 2);
        item.Property(x => x.Subtotal).HasPrecision(12, 2);
        var request = model.Entity<IdempotencyRequest>();
        request.HasKey(x => x.Id);
        request.Property(x => x.Key).HasMaxLength(100);
        request.Property(x => x.Operation).HasMaxLength(30);
        request.Property(x => x.RequestHash).HasMaxLength(64);
        request.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        request.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        request.HasIndex(x => new { x.UserId, x.Operation, x.Key }).IsUnique();
    }
}
