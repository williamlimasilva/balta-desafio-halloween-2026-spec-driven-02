namespace LinkShortener.Api.Contracts;

public sealed record CreateLinkHttpRequest(
    string? TargetUrl,
    string? Code = null,
    DateTime? ExpiresAtUtc = null);