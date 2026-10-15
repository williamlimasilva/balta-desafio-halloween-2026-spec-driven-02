using LinkShortener.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LinkShortener.Api.Data.Migrations;

[DbContext(typeof(LinkDbContext))]
public sealed class LinkDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity("LinkShortener.Api.Data.Link", link =>
        {
            link.Property<long>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasAnnotation("Sqlite:Autoincrement", true);

            link.Property<long>("AccessCount").HasColumnType("INTEGER");
            link.Property<string>("Code").IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            link.Property<DateTime>("CreatedAtUtc").HasColumnType("TEXT");
            link.Property<DateTime?>("ExpiresAtUtc").HasColumnType("TEXT");
            link.Property<string>("TargetUrl").IsRequired().HasMaxLength(2048).HasColumnType("TEXT");
            link.HasKey("Id");
            link.HasIndex("Code").IsUnique();
            link.ToTable("Links");
        });
    }
}