using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Api.Data;

public sealed class LinkRepository(LinkDbContext dbContext) : ILinkRepository
{
    public async Task AddAsync(Link link, CancellationToken cancellationToken = default)
    {
        dbContext.Links.Add(link);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSqliteUniqueConstraint(exception))
        {
            dbContext.Entry(link).State = EntityState.Detached;
            throw;
        }
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return dbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);
    }

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return dbContext.Links.AsNoTracking()
            .SingleOrDefaultAsync(link => link.Code == code, cancellationToken);
    }

    public Task<long?> GetAccessCountAsync(string code, CancellationToken cancellationToken = default)
    {
        return dbContext.Links.AsNoTracking()
            .Where(link => link.Code == code)
            .Select(link => (long?)link.AccessCount)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryIncrementAccessCountAsync(
        string code,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var updatedRows = await dbContext.Links
            .Where(link => link.Code == code && (link.ExpiresAtUtc == null || link.ExpiresAtUtc >= nowUtc))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(link => link.AccessCount, link => link.AccessCount + 1),
                cancellationToken);

        return updatedRows == 1;
    }

    private static bool IsSqliteUniqueConstraint(DbUpdateException exception)
    {
        for (var innerException = exception.InnerException;
             innerException is not null;
             innerException = innerException.InnerException)
        {
            if (innerException is SqliteException sqliteException &&
                sqliteException.SqliteExtendedErrorCode == 2067)
            {
                return true;
            }
        }

        return false;
    }
}