using LinkShortener.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Api.Services;

public sealed class LinkService : ILinkService
{
    private const string AllowedCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";
    private const int MinimumCustomCodeLength = 4;
    private const int MaximumCustomCodeLength = 20;
    private const int SqliteUniqueConstraint = 2067;

    private readonly ILinkRepository _repository;
    private readonly TargetUrlValidator _targetUrlValidator;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<string> _generateCode;

    public LinkService(
        ILinkRepository repository,
        TargetUrlValidator targetUrlValidator,
        Func<DateTime>? utcNow = null,
        Func<string>? generateCode = null)
    {
        _repository = repository;
        _targetUrlValidator = targetUrlValidator;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _generateCode = generateCode ?? CodeGenerator.GenerateCode;
    }

    public async Task<CreateLinkResult> CreateAsync(
        CreateLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _targetUrlValidator.IsValidAsync(request.TargetUrl, cancellationToken).ConfigureAwait(false))
        {
            return new CreateLinkResult.Invalid(
                "A URL de destino deve ser pública, usar HTTP ou HTTPS e ter no máximo 2048 caracteres.");
        }

        var nowUtc = AsUtc(_utcNow());
        DateTime? expiresAtUtc = null;
        if (request.ExpiresAtUtc is DateTime requestedExpiration)
        {
            if (requestedExpiration.Kind != DateTimeKind.Utc)
            {
                return new CreateLinkResult.Invalid("A data de expiração deve estar em UTC.");
            }

            if (requestedExpiration.Date < nowUtc.Date)
            {
                return new CreateLinkResult.Invalid("A data de expiração não pode estar no passado.");
            }

            expiresAtUtc = EndOfUtcDay(requestedExpiration.Date);
        }

        var hasCustomCode = request.Code is not null;
        if (hasCustomCode && !IsValidCustomCode(request.Code!))
        {
            return new CreateLinkResult.Invalid(
                "O código deve ter de 4 a 20 caracteres e conter apenas letras maiúsculas de A a Z e números de 1 a 9.");
        }

        while (true)
        {
            var code = hasCustomCode ? request.Code! : _generateCode();
            if (await _repository.CodeExistsAsync(code, cancellationToken).ConfigureAwait(false))
            {
                if (hasCustomCode)
                {
                    return new CreateLinkResult.Conflict("O código informado já está em uso.");
                }

                continue;
            }

            var link = new Link(code, request.TargetUrl!, nowUtc, expiresAtUtc);
            try
            {
                await _repository.AddAsync(link, cancellationToken).ConfigureAwait(false);
                return new CreateLinkResult.Created(link);
            }
            catch (DbUpdateException exception) when (IsSqliteUniqueConstraint(exception))
            {
                if (hasCustomCode)
                {
                    return new CreateLinkResult.Conflict("O código informado já está em uso.");
                }
            }
        }
    }

    public async Task<ResolveLinkResult> ResolveAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var link = await _repository.FindByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return new ResolveLinkResult.NotFound();
        }

        var nowUtc = AsUtc(_utcNow());
        if (link.ExpiresAtUtc is DateTime expiresAtUtc && nowUtc > expiresAtUtc)
        {
            return new ResolveLinkResult.Gone();
        }

        if (!await _repository.TryIncrementAccessCountAsync(code, nowUtc, cancellationToken).ConfigureAwait(false))
        {
            return new ResolveLinkResult.Gone();
        }

        return new ResolveLinkResult.Redirect(link.TargetUrl);
    }

    public async Task<LinkStatsResult> GetStatsAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var accessCount = await _repository.GetAccessCountAsync(code, cancellationToken).ConfigureAwait(false);
        return accessCount is long count
            ? new LinkStatsResult.Found(count)
            : new LinkStatsResult.NotFound();
    }

    private static bool IsValidCustomCode(string code)
    {
        return code.Length is >= MinimumCustomCodeLength and <= MaximumCustomCodeLength &&
               code.All(AllowedCodeCharacters.Contains);
    }

    private static DateTime EndOfUtcDay(DateTime dateUtc)
    {
        var date = DateTime.SpecifyKind(dateUtc.Date, DateTimeKind.Utc);
        return date == DateTime.MaxValue.Date
            ? DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc)
            : date.AddDays(1).AddTicks(-1);
    }

    private static DateTime AsUtc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static bool IsSqliteUniqueConstraint(DbUpdateException exception)
    {
        for (var innerException = exception.InnerException;
             innerException is not null;
             innerException = innerException.InnerException)
        {
            if (innerException is SqliteException sqliteException &&
                sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraint)
            {
                return true;
            }
        }

        return false;
    }
}