namespace LinkShortener.Api.Data;

public interface ILinkRepository
{
    Task AddAsync(Link link, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<long?> GetAccessCountAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> TryIncrementAccessCountAsync(string code, DateTime nowUtc, CancellationToken cancellationToken = default);
}