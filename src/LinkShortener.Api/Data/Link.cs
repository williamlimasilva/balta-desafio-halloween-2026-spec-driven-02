namespace LinkShortener.Api.Data;

public sealed class Link
{
    public long Id { get; private set; }
    public string Code { get; private set; }
    public string TargetUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public long AccessCount { get; private set; }

    private Link()
    {
        Code = string.Empty;
        TargetUrl = string.Empty;
    }

    public Link(string code, string targetUrl, DateTime createdAtUtc, DateTime? expiresAtUtc = null)
    {
        Code = code;
        TargetUrl = targetUrl;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }
}