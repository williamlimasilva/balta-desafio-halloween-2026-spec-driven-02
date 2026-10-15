using LinkShortener.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LinkShortener.Api.Data.Migrations;

[DbContext(typeof(LinkDbContext))]
[Migration("20261015000000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Links",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                AccessCount = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Links", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Links_Code",
            table: "Links",
            column: "Code",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Links");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
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