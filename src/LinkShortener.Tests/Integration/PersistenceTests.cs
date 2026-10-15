using LinkShortener.Api.Data;
using LinkShortener.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Migration_persists_links_across_context_restarts_and_enforces_unique_codes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        var options = CreateOptions(databasePath);

        try
        {
            await using (var firstContext = new LinkDbContext(options))
            {
                await firstContext.Database.MigrateAsync();
                var repository = new LinkRepository(firstContext);
                await repository.AddAsync(new Link("SAVE123", "https://example.com", DateTime.UtcNow));
                Assert.True(await repository.CodeExistsAsync("SAVE123"));
            }

            await using (var restartedContext = new LinkDbContext(options))
            {
                var repository = new LinkRepository(restartedContext);
                var persistedLink = await repository.FindByCodeAsync("SAVE123");

                Assert.NotNull(persistedLink);
                Assert.Equal("https://example.com", persistedLink.TargetUrl);
                Assert.Equal(0, await repository.GetAccessCountAsync("SAVE123"));
                await Assert.ThrowsAsync<DbUpdateException>(() =>
                    repository.AddAsync(new Link("SAVE123", "https://other.example", DateTime.UtcNow)));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Link_service_retries_real_unique_collision_without_replaying_failed_entity()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        var options = CreateOptions(databasePath);

        try
        {
            await using var context = new LinkDbContext(options);
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER CreateCodeCollision
                BEFORE INSERT ON Links
                WHEN NEW.Code = 'COLLIDE'
                BEGIN
                    INSERT INTO Links (Code, TargetUrl, CreatedAtUtc, AccessCount)
                    VALUES ('COLLIDE', 'https://race.example', '2026-10-15 00:00:00', 0);
                END;
                """);

            var generatedCodes = new Queue<string>(new[] { "COLLIDE", "NEXT999" });
            var service = new LinkService(
                new LinkRepository(context),
                new TargetUrlValidator(new StubDnsResolver()),
                () => new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                generatedCodes.Dequeue);

            var result = await service.CreateAsync(new CreateLinkRequest("https://93.184.216.34/destination"));

            var created = Assert.IsType<CreateLinkResult.Created>(result);
            Assert.Equal("NEXT999", created.Link.Code);
            Assert.Empty(generatedCodes);
            Assert.Equal("NEXT999", await context.Links.Select(link => link.Code).SingleAsync());
            Assert.DoesNotContain(context.ChangeTracker.Entries<Link>(), entry => entry.Entity.Code == "COLLIDE");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Link_service_propagates_non_unique_sqlite_constraint_without_retry()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        var options = CreateOptions(databasePath);

        try
        {
            await using var context = new LinkDbContext(options);
            await context.Database.MigrateAsync();
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER RejectCode
                BEFORE INSERT ON Links
                WHEN NEW.Code = 'FAIL123'
                BEGIN
                    SELECT RAISE(ABORT, 'non-unique write failure');
                END;
                """);

            var generationCount = 0;
            var service = new LinkService(
                new LinkRepository(context),
                new TargetUrlValidator(new StubDnsResolver()),
                () => new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                () =>
                {
                    generationCount++;
                    return generationCount == 1 ? "FAIL123" : "NEXT999";
                });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.CreateAsync(new CreateLinkRequest("https://93.184.216.34/destination")));

            var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
            Assert.NotEqual(2067, sqliteException.SqliteExtendedErrorCode);
            Assert.Equal(1, generationCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Conditional_atomic_increment_counts_only_active_links()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        var options = CreateOptions(databasePath);
        var nowUtc = DateTime.UtcNow;

        try
        {
            await using var context = new LinkDbContext(options);
            await context.Database.MigrateAsync();
            var repository = new LinkRepository(context);
            await repository.AddAsync(new Link("LIVE123", "https://example.com", nowUtc, nowUtc.AddMinutes(1)));
            await repository.AddAsync(new Link("OLD1234", "https://example.com", nowUtc, nowUtc.AddMinutes(-1)));

            Assert.True(await repository.TryIncrementAccessCountAsync("LIVE123", nowUtc));
            Assert.True(await repository.TryIncrementAccessCountAsync("LIVE123", nowUtc));
            Assert.False(await repository.TryIncrementAccessCountAsync("OLD1234", nowUtc));
            Assert.False(await repository.TryIncrementAccessCountAsync("MISSING", nowUtc));
            Assert.Equal(2, await repository.GetAccessCountAsync("LIVE123"));
            Assert.Equal(0, await repository.GetAccessCountAsync("OLD1234"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Concurrent_inserts_with_the_same_code_allow_exactly_one_link()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        var options = CreateOptions(databasePath);

        try
        {
            await using (var context = new LinkDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            var attempts = Enumerable.Range(0, 8).Select(async _ =>
            {
                await using var context = new LinkDbContext(options);
                try
                {
                    await new LinkRepository(context).AddAsync(
                        new Link("RACE123", "https://example.com", DateTime.UtcNow));
                    return true;
                }
                catch (DbUpdateException)
                {
                    return false;
                }
            });

            var results = await Task.WhenAll(attempts);

            Assert.Single(results, inserted => inserted);
            Assert.Equal(7, results.Count(inserted => !inserted));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static DbContextOptions<LinkDbContext> CreateOptions(string databasePath)
    {
        return new DbContextOptionsBuilder<LinkDbContext>()
            .UseSqlite($"Data Source={databasePath};Default Timeout=30")
            .Options;
    }

    private sealed class StubDnsResolver : IDnsResolver
    {
        public Task<System.Net.IPAddress[]> ResolveAsync(
            string hostName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Array.Empty<System.Net.IPAddress>());
        }
    }
}