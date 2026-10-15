using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace LinkShortener.Tests.Integration;

public sealed class CreateLinkTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_link_returns_created_short_url_and_code()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "https://93.184.216.34/destination"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateLinkResponse>();
        Assert.NotNull(body);
        Assert.Equal(7, body.Code.Length);
        Assert.All(body.Code, character => Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789"));
        Assert.Equal($"{client.BaseAddress!.GetLeftPart(UriPartial.Authority)}/{body.Code}", body.ShortUrl);
    }

    [Fact]
    public async Task Create_link_with_past_expiration_returns_400_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "https://93.184.216.34/destination",
            expiresAtUtc = new DateTime(2026, 10, 14, 23, 0, 0, DateTimeKind.Utc)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("passado", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_link_with_non_http_target_returns_400_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/links", new
        {
            targetUrl = "ftp://93.184.216.34/file"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task Create_link_with_duplicate_custom_code_returns_409_problem_details()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            targetUrl = "https://93.184.216.34/destination",
            code = "UNIQ123"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/links", request);
        var duplicateResponse = await client.PostAsJsonAsync("/api/links", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
    }

    private sealed record CreateLinkResponse(string ShortUrl, string Code);
}