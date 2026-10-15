using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using LinkShortener.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinkShortener.Tests.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly bool _ownsDatabase;

    public DateTime UtcNow { get; set; } = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    public ApiFactory() : this(null)
    {
    }

    internal ApiFactory(string? databasePath)
    {
        _databasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");
        _ownsDatabase = databasePath is null;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath};Default Timeout=30");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDnsResolver>();
            services.AddSingleton<IDnsResolver, StubDnsResolver>();
            services.RemoveAll<Func<DateTime>>();
            services.AddSingleton<Func<DateTime>>(_ => () => UtcNow);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            if (_ownsDatabase && File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }

    private sealed class StubDnsResolver : IDnsResolver
    {
        public Task<IPAddress[]> ResolveAsync(
            string hostName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
        }
    }
}