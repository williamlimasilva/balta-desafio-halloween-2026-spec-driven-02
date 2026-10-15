using LinkShortener.Api.Contracts;
using LinkShortener.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/links", CreateAsync);
        endpoints.MapGet("/{code}", RedirectAsync);
        endpoints.MapGet("/api/links/{code}/stats", GetStatsAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateLinkHttpRequest request,
        HttpContext httpContext,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.CreateAsync(
            new CreateLinkRequest(request.TargetUrl, request.Code, request.ExpiresAtUtc),
            cancellationToken);

        return result switch
        {
            CreateLinkResult.Created created => Results.Created(
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{created.Link.Code}",
                new CreateLinkHttpResponse(
                    $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{created.Link.Code}",
                    created.Link.Code)),
            CreateLinkResult.Invalid invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Requisição inválida",
                detail: invalid.Detail),
            CreateLinkResult.Conflict conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Código já utilizado",
                detail: conflict.Detail),
            _ => throw new InvalidOperationException("Resultado de criação de link desconhecido.")
        };
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.ResolveAsync(code, cancellationToken);
        return result switch
        {
            ResolveLinkResult.Redirect redirect => Results.Redirect(redirect.TargetUrl),
            ResolveLinkResult.NotFound => Results.NotFound(),
            ResolveLinkResult.Gone => Results.StatusCode(StatusCodes.Status410Gone),
            _ => throw new InvalidOperationException("Resultado de resolução de link desconhecido.")
        };
    }

    private static async Task<IResult> GetStatsAsync(
        string code,
        ILinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.GetStatsAsync(code, cancellationToken);
        return result switch
        {
            LinkStatsResult.Found found => Results.Ok(new LinkStatsHttpResponse(code, found.AccessCount)),
            LinkStatsResult.NotFound => Results.NotFound(),
            _ => throw new InvalidOperationException("Resultado de estatísticas de link desconhecido.")
        };
    }
}