using LinkShortener.Api.Data;

namespace LinkShortener.Api.Services;

public sealed record CreateLinkRequest(
    string? TargetUrl,
    string? Code = null,
    DateTime? ExpiresAtUtc = null);

public abstract record CreateLinkResult
{
    public sealed record Created(Link Link) : CreateLinkResult;
    public sealed record Invalid(string Detail) : CreateLinkResult;
    public sealed record Conflict(string Detail) : CreateLinkResult;
}

public abstract record ResolveLinkResult
{
    public sealed record Redirect(string TargetUrl) : ResolveLinkResult;
    public sealed record NotFound : ResolveLinkResult;
    public sealed record Gone : ResolveLinkResult;
}

public abstract record LinkStatsResult
{
    public sealed record Found(long AccessCount) : LinkStatsResult;
    public sealed record NotFound : LinkStatsResult;
}