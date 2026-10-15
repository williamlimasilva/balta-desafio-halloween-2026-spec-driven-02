namespace LinkShortener.Api.Contracts;

public sealed record CreateLinkHttpResponse(string ShortUrl, string Code);