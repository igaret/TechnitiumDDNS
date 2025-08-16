using DynamicDns.Models;
using Microsoft.EntityFrameworkCore;

namespace DynamicDns.Data
{
    public class DynamicDnsDbContext : DbContext
    {
        public DynamicDnsDbContext(DbContextOptions<DynamicDnsDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<DynamicDnsEntry> DynamicDnsEntries { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasMany(u => u.DynamicDnsEntries)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.ApiKeys)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.PaymentTransactions)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure DynamicDnsEntry entity
            modelBuilder.Entity<DynamicDnsEntry>()
                .HasIndex(d => d.DomainName)
                .IsUnique();

            modelBuilder.Entity<DynamicDnsEntry>()
                .HasIndex(d => d.UpdateToken)
                .IsUnique();

            // Configure ApiKey entity
            modelBuilder.Entity<ApiKey>()
                .HasIndex(a => a.Key)
                .IsUnique();

            // Configure PaymentTransaction entity
            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(p => p.TransactionId)
                .IsUnique();
        }
    }
}