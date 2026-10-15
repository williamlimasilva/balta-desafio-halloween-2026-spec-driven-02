using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class PersistenceAcrossRestartTests
{
    [Fact]
    public async Task Created_link_and_access_count_survive_a_fresh_api_host()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"linkshortener-restart-{Guid.NewGuid():N}.db");

        try
        {
            using (var firstFactory = new ApiFactory(databasePath))
            using (var firstClient = firstFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                var createResponse = await firstClient.PostAsJsonAsync("/api/links", new
                {
                    targetUrl = "https://93.184.216.34/destination",
                    code = "KEEP123"
                });
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                using var accessResponse = await firstClient.GetAsync("/KEEP123");
                Assert.Equal(HttpStatusCode.Redirect, accessResponse.StatusCode);
            }

            using (var restartedFactory = new ApiFactory(databasePath))
            using (var restartedClient = restartedFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                using var redirectResponse = await restartedClient.GetAsync("/KEEP123");
                using var statsResponse = await restartedClient.GetAsync("/api/links/KEEP123/stats");
                var stats = await statsResponse.Content.ReadFromJsonAsync<LinkStatsResponse>();

                Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
                Assert.NotNull(stats);
                Assert.Equal(2, stats.AccessCount);
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

    private sealed record LinkStatsResponse(string Code, long AccessCount);
}