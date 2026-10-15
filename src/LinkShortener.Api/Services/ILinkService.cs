using LinkShortener.Api.Data;

namespace LinkShortener.Api.Services;

public interface ILinkService
{
    Task<CreateLinkResult> CreateAsync(
        CreateLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<ResolveLinkResult> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<LinkStatsResult> GetStatsAsync(
        string code,
        CancellationToken cancellationToken = default);
}