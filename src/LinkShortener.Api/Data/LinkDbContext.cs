using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Api.Data;

public sealed class LinkDbContext(DbContextOptions<LinkDbContext> options) : DbContext(options)
{
    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<Link>();

        link.ToTable("Links");
        link.HasKey(item => item.Id);
        link.Property(item => item.Code).HasMaxLength(20).IsRequired();
        link.Property(item => item.TargetUrl).HasMaxLength(2048).IsRequired();
        link.Property(item => item.CreatedAtUtc).IsRequired();
        link.Property(item => item.AccessCount).IsRequired();
        link.HasIndex(item => item.Code).IsUnique();
    }
}