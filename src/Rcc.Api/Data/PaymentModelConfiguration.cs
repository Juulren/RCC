using Microsoft.EntityFrameworkCore;

namespace Rcc.Api.Data;

public static class PaymentModelConfiguration
{
    public static void ConfigurePayments(this ModelBuilder model)
    {
        var payment = model.Entity<Payment>();
        payment.HasKey(x => x.Id);
        payment.HasOne(x => x.Reservation).WithMany(x => x.Payments).HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        payment.HasIndex(x => x.ReservationId).IsUnique();
        payment.HasIndex(x => x.SessionId).IsUnique();
        payment.HasIndex(x => x.PaymentIntentId).IsUnique();
        payment.HasIndex(x => new { x.State, x.NextSyncAt });
        payment.Property(x => x.SessionId).HasMaxLength(255);
        payment.Property(x => x.PaymentIntentId).HasMaxLength(255);
        payment.Property(x => x.State).HasMaxLength(20);
        payment.Property(x => x.Currency).HasMaxLength(3);
        payment.Property(x => x.ReturnOrigin).HasMaxLength(500);
        payment.Property(x => x.LastError).HasMaxLength(80);
        payment.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "\"AmountMinor\" BETWEEN 1 AND 99999999");
            table.HasCheckConstraint("CK_Payments_Currency", "\"Currency\" = 'cny'");
            table.HasCheckConstraint("CK_Payments_State", "\"State\" IN ('Creating','Open','Paid','Expired','Review','Refunded')");
        });
        var events = model.Entity<PaymentEvent>();
        events.HasKey(x => x.Id);
        events.Property(x => x.Id).HasMaxLength(255);
        events.Property(x => x.Type).HasMaxLength(100);
        events.Property(x => x.Outcome).HasMaxLength(80);
        events.HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        var job = model.Entity<RefundJob>();
        job.HasKey(x => x.Id);
        job.HasOne(x => x.Payment).WithOne(x => x.Refund).HasForeignKey<RefundJob>(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        job.Property(x => x.StripeRefundId).HasMaxLength(255);
        job.HasIndex(x => x.StripeRefundId).IsUnique();
        job.Property(x => x.State).HasMaxLength(20);
        job.Property(x => x.LastError).HasMaxLength(80);
        job.HasIndex(x => new { x.State, x.NextAttemptAt });
        job.ToTable("RefundJobs", table =>
        {
            table.HasCheckConstraint("CK_RefundJobs_Amount", "\"AmountMinor\" > 0");
            table.HasCheckConstraint("CK_RefundJobs_State", "\"State\" IN ('Pending','Processing','Succeeded','Failed','Review')");
        });
    }
}
