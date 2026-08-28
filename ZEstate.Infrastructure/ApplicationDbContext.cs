using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ZEstate.Infrastructure.Data.IdentityModels;
using ZEstate.Infrastructure.Data.Models;
namespace ZEstate.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Npgsql maps DateTime to PostgreSQL's "timestamp with time zone" and, since
        // v6, strictly rejects writing a DateTime whose Kind isn't Utc (JSON-bound
        // DateTime values from request bodies come in as Kind=Unspecified, since the
        // frontend sends naive "yyyy-MM-ddTHH:mm" strings with no offset). Without
        // this, any create/update touching a DateTime property (meetings, fees,
        // vote windows, etc.) throws and 500s. This forces every DateTime/DateTime?
        // property in the model to Kind=Utc on write, model-wide, instead of fixing
        // it property-by-property.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApartmentUser>()
                .HasKey(au => new { au.UserId, au.ApartmentId });

            // One vote per apartment per question - ideal parts is the apartment's
            // weight, so a second vote from the same apartment must be rejected.
            builder.Entity<Vote>()
                .HasIndex(v => new { v.VoteQuestionId, v.ApartmentId })
                .IsUnique();
        }

        public DbSet<Building> Buildings { get; set; }
        public DbSet<Apartment> Apartments { get; set; }
        public DbSet<ApartmentUser> ApartmentUsers { get; set; }
        public DbSet<JoinRequest> JoinRequests { get; set; }
        public DbSet<Fee> Fees { get; set; }
        public DbSet<Meeting> Meetings { get; set; }
        public DbSet<Obligation> Obligations { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Repair> Repairs { get; set; }
        public DbSet<Vote> Votes { get; set; }
        public DbSet<VoteQuestion> VoteQuestions { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<InviteCodeLog> InviteCodeLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ApartmentTransferLog> ApartmentTransferLogs { get; set; }
        public DbSet<CashLedgerEntry> CashLedgerEntries { get; set; }
    }

    // Relabels a DateTime as Kind=Utc without shifting the wall-clock value - these
    // are naive instants throughout the app (DateTime.UtcNow, or user-entered dates
    // with no timezone concept), not genuine timezone conversions.
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }

    public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter() : base(
            v => v.HasValue ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        {
        }
    }
}
