using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Eventizo.Models;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Eventizo.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // --- DbSets ---
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventType> EventTypes { get; set; }
        public DbSet<EventImage> EventImages { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Payment2> Payment2s { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<WaitingRoom> WaitingRooms { get; set; }
        public DbSet<ChatHistory> ChatHistories { get; set; }
        public DbSet<EventRating> EventRatings { get; set; }
        public DbSet<FavoriteVote> FavoriteVotes { get; set; }
        public DbSet<UserPointHistory> UserPointHistories { get; set; }
        public DbSet<OccupiedSeat> OccupiedSeats { get; set; }
        public DbSet<PendingPaymentSeat> PendingPaymentSeats { get; set; }
        public DbSet<UserPoint> UserPoints { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Ticket ---
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Event)
                .WithMany(e => e.Tickets)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- ChatMessage ---
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- Conversation ---
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Admin)
                .WithMany()
                .HasForeignKey(c => c.AdminId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.PreviousOwner)
                .WithMany()
                .HasForeignKey(t => t.PreviousOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.NewOwner)
                .WithMany()
                .HasForeignKey(t => t.NewOwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        // ===== Override SaveChanges để UTC+7 cho ChatMessage =====
        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<ChatMessage>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow.AddHours(7);
                    entry.Entity.SentAt = DateTime.UtcNow.AddHours(7);
                }
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<ChatMessage>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow.AddHours(7);
                    entry.Entity.SentAt = DateTime.UtcNow.AddHours(7);
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
