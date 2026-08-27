using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    }
}
