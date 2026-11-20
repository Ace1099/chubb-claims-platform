using ClaimsPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Data;

public class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<InfoRequest> InfoRequests => Set<InfoRequest>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ClaimEvent> ClaimEvents => Set<ClaimEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums stored as readable text, not numbers.
        builder.Properties<ClaimStatus>().HaveConversion<string>();
        builder.Properties<ClaimType>().HaveConversion<string>();
        builder.Properties<Market>().HaveConversion<string>();
        builder.Properties<UserRole>().HaveConversion<string>();
        // SQLite has no real decimal type; store money as a number. Production: decimal(18,2) in SQL Server/Postgres.
        builder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // IDs are generated in code, so tell EF never to generate them
        // (otherwise new child rows can be mistaken for updates).
        model.Entity<User>().Property(x => x.Id).ValueGeneratedNever();
        model.Entity<InfoRequest>().Property(x => x.Id).ValueGeneratedNever();
        model.Entity<Decision>().Property(x => x.Id).ValueGeneratedNever();
        model.Entity<ClaimEvent>().Property(x => x.Id).ValueGeneratedNever();
        model.Entity<ClaimEvent>().Property(x => x.FromStatus).HasConversion<string>();

        model.Entity<Notification>(e =>
        {
            e.Property(n => n.Id).ValueGeneratedNever();
            e.HasIndex(n => n.SourceEventId).IsUnique();
            e.HasIndex(n => n.UserId);
        });

        model.Entity<Claim>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Version).IsConcurrencyToken(); // two officers can't both win a race

            e.HasOne(c => c.Claimant).WithMany()
                .HasForeignKey(c => c.ClaimantId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.AssignedOfficer).WithMany()
                .HasForeignKey(c => c.AssignedOfficerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Decision).WithOne().HasForeignKey<Decision>(d => d.ClaimId);
            e.HasMany(c => c.InfoRequests).WithOne().HasForeignKey(i => i.ClaimId);
            e.HasMany(c => c.Events).WithOne().HasForeignKey(ev => ev.ClaimId);

            // Indexes for queries we know we'll run (officer queue, dashboards).
            e.HasIndex(c => c.ClaimNumber).IsUnique();
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.AssignedOfficerId);
            e.HasIndex(c => new { c.Market, c.Type });
        });
    }
}