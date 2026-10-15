using System.Net;
using LinkShortener.Api.Data;
using LinkShortener.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinkShortener.Tests.Unit;

public class LinkServiceTests
{
    private const string TargetUrl = "https://example.com/destination";
    private const string AllowedCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    [Fact]
    public async Task CreateAsync_GeneratesValidCodeAndLeavesExpirationUnset()
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15), () => "ABC1234");

        var result = await service.CreateAsync(new CreateLinkRequest(TargetUrl));

        var created = Assert.IsType<CreateLinkResult.Created>(result);
        Assert.Equal("ABC1234", created.Link.Code);
        Assert.Equal(7, created.Link.Code.Length);
        Assert.All(created.Link.Code, character => Assert.Contains(character, AllowedCodeCharacters));
        Assert.Null(created.Link.ExpiresAtUtc);
        Assert.Equal(Utc(2026, 10, 15), created.Link.CreatedAtUtc);
    }

    [Theory]
    [InlineData("ABCD")]
    [InlineData("ABCDEFGHIJKLMNOPQRST")]
    public async Task CreateAsync_AcceptsCustomCodeLengthBoundaries(string code)
    {
        var service = CreateService(new MemoryLinkRepository(), Utc(2026, 10, 15));

        var result = await service.CreateAsync(new CreateLinkRequest(TargetUrl, code));

        Assert.Equal(code, Assert.IsType<CreateLinkResult.Created>(result).Link.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("abc123")]
    [InlineData("AB0D")]
    [InlineData("AB D")]
    [InlineData("AB-D")]
    public async Task CreateAsync_RejectsCustomCodesOutsideAllowedFormat(string code)
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.CreateAsync(new CreateLinkRequest(TargetUrl, code));

        Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidTargetUrl()
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.CreateAsync(new CreateLinkRequest("ftp://example.com/file"));

        Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_RejectsExpirationBeforeCurrentUtcDate()
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15, 0, 0));

        var result = await service.CreateAsync(
            new CreateLinkRequest(TargetUrl, ExpiresAtUtc: Utc(2026, 10, 14, 23, 59)));

        Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_RejectsExpirationThatIsNotUtc()
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15));
        var unspecifiedDate = new DateTime(2026, 10, 16);

        var result = await service.CreateAsync(
            new CreateLinkRequest(TargetUrl, ExpiresAtUtc: unspecifiedDate));

        Assert.IsType<CreateLinkResult.Invalid>(result);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_RejectsCustomCodeAlreadyInUse()
    {
        var repository = new MemoryLinkRepository();
        repository.Seed(new Link("KNOWN", TargetUrl, Utc(2026, 10, 1)));
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.CreateAsync(new CreateLinkRequest(TargetUrl, "KNOWN"));

        Assert.IsType<CreateLinkResult.Conflict>(result);
        Assert.Single(repository.Links);
    }

    [Fact]
    public async Task CreateAsync_RetriesGeneratedCodeAfterDatabaseUniqueCollision()
    {
        var repository = new MemoryLinkRepository();
        repository.Seed(new Link("COLLIDE", TargetUrl, Utc(2026, 10, 1)));
        var raceRepository = new RaceOnFirstCodeRepository(repository, "COLLIDE", CreateUniqueConstraintException());
        var generatedCodes = new Queue<string>(new[] { "COLLIDE", "NEXT999" });
        var service = CreateService(raceRepository, Utc(2026, 10, 15), generatedCodes.Dequeue);

        var result = await service.CreateAsync(new CreateLinkRequest(TargetUrl));

        var created = Assert.IsType<CreateLinkResult.Created>(result);
        Assert.Equal("NEXT999", created.Link.Code);
        Assert.Equal(2, raceRepository.AddAttempts);
        Assert.Equal(1, repository.Links.Count(link => link.Code == "NEXT999"));
    }

    [Fact]
    public async Task CreateAsync_DoesNotRetryUnrelatedDatabaseUpdateException()
    {
        var repository = new MemoryLinkRepository
        {
            AddException = new DbUpdateException("Database write failed.")
        };
        var service = CreateService(repository, Utc(2026, 10, 15), () => "ABC1234");

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.CreateAsync(new CreateLinkRequest(TargetUrl)));

        Assert.Equal(1, repository.AddAttempts);
    }

    [Fact]
    public async Task ResolveAsync_ExpirationIsInclusiveThroughEndOfUtcDate()
    {
        var repository = new MemoryLinkRepository();
        var nowUtc = Utc(2026, 10, 15, 12, 0);
        var service = CreateService(repository, () => nowUtc, () => "ABC1234");
        var created = Assert.IsType<CreateLinkResult.Created>(await service.CreateAsync(
            new CreateLinkRequest(TargetUrl, ExpiresAtUtc: Utc(2026, 10, 16, 8, 0))));
        var lastTickOfDate = Utc(2026, 10, 16, 23, 59, 59).AddTicks(9_999_999);
        Assert.Equal(lastTickOfDate, created.Link.ExpiresAtUtc);

        nowUtc = lastTickOfDate;
        var validResult = await service.ResolveAsync(created.Link.Code);
        nowUtc = lastTickOfDate.AddTicks(1);
        var expiredResult = await service.ResolveAsync(created.Link.Code);

        Assert.IsType<ResolveLinkResult.Redirect>(validResult);
        Assert.IsType<ResolveLinkResult.Gone>(expiredResult);
        Assert.Equal(1, await repository.GetAccessCountAsync(created.Link.Code));
    }

    [Fact]
    public async Task ResolveAsync_LinkWithoutExpirationRemainsValid()
    {
        var repository = new MemoryLinkRepository();
        var nowUtc = Utc(2026, 10, 15);
        var service = CreateService(repository, () => nowUtc, () => "ABC1234");
        var created = Assert.IsType<CreateLinkResult.Created>(
            await service.CreateAsync(new CreateLinkRequest(TargetUrl)));
        nowUtc = Utc(2100, 1, 1);

        var result = await service.ResolveAsync(created.Link.Code);

        Assert.Equal(TargetUrl, Assert.IsType<ResolveLinkResult.Redirect>(result).TargetUrl);
        Assert.Equal(1, await repository.GetAccessCountAsync(created.Link.Code));
    }

    [Fact]
    public async Task ResolveAsync_DoesNotIncrementExpiredLink()
    {
        var repository = new MemoryLinkRepository();
        repository.Seed(new Link("EXPIRE", TargetUrl, Utc(2026, 10, 1), Utc(2026, 10, 14, 23, 59, 59).AddTicks(9_999_999)));
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.ResolveAsync("EXPIRE");

        Assert.IsType<ResolveLinkResult.Gone>(result);
        Assert.Equal(0, await repository.GetAccessCountAsync("EXPIRE"));
        Assert.Equal(0, repository.IncrementAttempts);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNotFoundForUnknownCode()
    {
        var repository = new MemoryLinkRepository();
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.ResolveAsync("MISSING");

        Assert.IsType<ResolveLinkResult.NotFound>(result);
        Assert.Equal(0, repository.IncrementAttempts);
    }

    [Fact]
    public async Task ResolveAsync_DelegatesAtomicIncrementsWithoutDecreasingCount()
    {
        var repository = new MemoryLinkRepository();
        repository.Seed(new Link("COUNT", TargetUrl, Utc(2026, 10, 1)), 7);
        var service = CreateService(repository, Utc(2026, 10, 15));

        var firstResult = await service.ResolveAsync("COUNT");
        var secondResult = await service.ResolveAsync("COUNT");

        Assert.IsType<ResolveLinkResult.Redirect>(firstResult);
        Assert.IsType<ResolveLinkResult.Redirect>(secondResult);
        Assert.Equal(9, await repository.GetAccessCountAsync("COUNT"));
        Assert.Equal(2, repository.IncrementAttempts);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCountForPubliclyKnownCode()
    {
        var repository = new MemoryLinkRepository();
        repository.Seed(new Link("STATS", TargetUrl, Utc(2026, 10, 1)), 12);
        var service = CreateService(repository, Utc(2026, 10, 15));

        var result = await service.GetStatsAsync("STATS");

        Assert.Equal(12, Assert.IsType<LinkStatsResult.Found>(result).AccessCount);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsNotFoundForUnknownCode()
    {
        var service = CreateService(new MemoryLinkRepository(), Utc(2026, 10, 15));

        var result = await service.GetStatsAsync("MISSING");

        Assert.IsType<LinkStatsResult.NotFound>(result);
    }

    private static LinkService CreateService(
        ILinkRepository repository,
        DateTime nowUtc,
        Func<string>? generateCode = null)
    {
        return CreateService(repository, () => nowUtc, generateCode);
    }

    private static LinkService CreateService(
        ILinkRepository repository,
        Func<DateTime> utcNow,
        Func<string>? generateCode = null)
    {
        return new LinkService(repository, CreateValidator(), utcNow, generateCode);
    }

    private static TargetUrlValidator CreateValidator()
    {
        return new TargetUrlValidator(new StubDnsResolver(IPAddress.Parse("93.184.216.34")));
    }

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }

    private static DbUpdateException CreateUniqueConstraintException()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Codes (Code TEXT UNIQUE); INSERT INTO Codes (Code) VALUES ('COLLIDE');";
        command.ExecuteNonQuery();
        command.CommandText = "INSERT INTO Codes (Code) VALUES ('COLLIDE');";
        var sqliteException = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        return new DbUpdateException("Unique code violation.", sqliteException);
    }

    private sealed class StubDnsResolver(params IPAddress[] addresses) : IDnsResolver
    {
        public Task<IPAddress[]> ResolveAsync(string hostName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(addresses);
        }
    }

    private sealed class MemoryLinkRepository : ILinkRepository
    {
        private readonly Dictionary<string, Link> _links = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _accessCounts = new(StringComparer.Ordinal);

        public List<Link> Links => _links.Values.ToList();
        public DbUpdateException? AddException { get; set; }
        public int AddAttempts { get; private set; }
        public int IncrementAttempts { get; private set; }

        public void Seed(Link link, long accessCount = 0)
        {
            _links.Add(link.Code, link);
            _accessCounts.Add(link.Code, accessCount);
        }

        public Task AddAsync(Link link, CancellationToken cancellationToken = default)
        {
            AddAttempts++;
            if (AddException is not null)
            {
                var exception = AddException;
                AddException = null;
                throw exception;
            }

            _links.Add(link.Code, link);
            _accessCounts.Add(link.Code, 0);
            return Task.CompletedTask;
        }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_links.ContainsKey(code));
        }

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            _links.TryGetValue(code, out var link);
            return Task.FromResult(link);
        }

        public Task<long?> GetAccessCountAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_accessCounts.TryGetValue(code, out var count) ? count : (long?)null);
        }

        public Task<bool> TryIncrementAccessCountAsync(
            string code,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            IncrementAttempts++;
            if (!_links.TryGetValue(code, out var link) ||
                (link.ExpiresAtUtc is DateTime expiration && expiration < nowUtc))
            {
                return Task.FromResult(false);
            }

            _accessCounts[code]++;
            return Task.FromResult(true);
        }
    }

    private sealed class RaceOnFirstCodeRepository(
        MemoryLinkRepository inner,
        string collidingCode,
        DbUpdateException uniqueConstraintException) : ILinkRepository
    {
        private bool _firstCodeCheck = true;
        private bool _firstAdd = true;

        public int AddAttempts { get; private set; }

        public Task AddAsync(Link link, CancellationToken cancellationToken = default)
        {
            AddAttempts++;
            if (_firstAdd && link.Code == collidingCode)
            {
                _firstAdd = false;
                throw uniqueConstraintException;
            }

            return inner.AddAsync(link, cancellationToken);
        }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            if (_firstCodeCheck && code == collidingCode)
            {
                _firstCodeCheck = false;
                return Task.FromResult(false);
            }

            return inner.CodeExistsAsync(code, cancellationToken);
        }

        public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return inner.FindByCodeAsync(code, cancellationToken);
        }

        public Task<long?> GetAccessCountAsync(string code, CancellationToken cancellationToken = default)
        {
            return inner.GetAccessCountAsync(code, cancellationToken);
        }

        public Task<bool> TryIncrementAccessCountAsync(
            string code,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            return inner.TryIncrementAccessCountAsync(code, nowUtc, cancellationToken);
        }
    }
}