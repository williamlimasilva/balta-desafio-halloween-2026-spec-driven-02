using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class ConcurrentAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Concurrent_http_accesses_are_all_counted_in_sqlite()
    {
        const int requestCount = 24;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var createResponse = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "https://93.184.216.34/destination",
            code = "RACE123"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, requestCount).Select(_ => client.GetAsync("/RACE123")));
        using var statsResponse = await client.GetAsync("/api/links/RACE123/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<LinkStatsResponse>();

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        Assert.NotNull(stats);
        Assert.Equal(requestCount, stats.AccessCount);

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private sealed record LinkStatsResponse(string Code, long AccessCount);
}