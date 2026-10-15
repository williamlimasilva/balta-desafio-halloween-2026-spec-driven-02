using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class StatsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Public_stats_report_repeated_accesses_without_an_api_key()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var createResponse = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "https://93.184.216.34/destination",
            code = "STATS12"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var firstAccess = await client.GetAsync("/STATS12");
        using var secondAccess = await client.GetAsync("/STATS12");
        using var automatedRequest = new HttpRequestMessage(HttpMethod.Get, "/STATS12");
        automatedRequest.Headers.UserAgent.ParseAdd("integration-bot/1.0");
        using var automatedAccess = await client.SendAsync(automatedRequest);
        using var statsResponse = await client.GetAsync("/api/links/STATS12/stats");
        var stats = await statsResponse.Content.ReadFromJsonAsync<LinkStatsResponse>();

        Assert.Equal(HttpStatusCode.Redirect, firstAccess.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, secondAccess.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, automatedAccess.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        Assert.NotNull(stats);
        Assert.Equal("STATS12", stats.Code);
        Assert.Equal(3, stats.AccessCount);
    }

    [Fact]
    public async Task Public_stats_for_unknown_code_returns_404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/links/MISSING/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record LinkStatsResponse(string Code, long AccessCount);
}