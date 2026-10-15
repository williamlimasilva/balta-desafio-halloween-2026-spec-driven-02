using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class RedirectTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Existing_link_returns_302_without_following_redirect()
    {
        factory.UtcNow = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CreateLinkAsync(client, "LIVE123");

        using var response = await client.GetAsync("/LIVE123");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://93.184.216.34/destination", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Missing_link_returns_404()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/MISSING");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_is_valid_through_utc_expiration_day_then_returns_410_without_counting()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CreateLinkAsync(client, "DAY1234", new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc));

        factory.UtcNow = new DateTime(2026, 10, 15, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999);
        using var lastValidResponse = await client.GetAsync("/DAY1234");
        factory.UtcNow = factory.UtcNow.AddTicks(1);
        using var expiredResponse = await client.GetAsync("/DAY1234");
        using var statsResponse = await client.GetAsync("/api/links/DAY1234/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<LinkStatsResponse>();

        Assert.Equal(HttpStatusCode.Redirect, lastValidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, expiredResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        Assert.NotNull(stats);
        Assert.Equal(1, stats.AccessCount);
    }

    private static async Task CreateLinkAsync(HttpClient client, string code, DateTime? expiresAtUtc = null)
    {
        var response = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "https://93.184.216.34/destination",
            code,
            expiresAtUtc
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private sealed record LinkStatsResponse(string Code, long AccessCount);
}