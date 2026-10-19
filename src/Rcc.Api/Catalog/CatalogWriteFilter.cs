using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Rcc.Api.Catalog;

public sealed class CatalogWriteFilter(IAntiforgery csrf) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            try { await csrf.ValidateRequestAsync(context.HttpContext); }
            catch (AntiforgeryValidationException) { return CatalogEndpoints.Error(400, "CSRF_INVALID"); }
        }
        try { return await next(context); }
        catch (DbUpdateConcurrencyException) { return CatalogEndpoints.Error(409, "CATALOG_CHANGED"); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return CatalogEndpoints.Error(409, "DUPLICATE_CATALOG_KEY"); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { return CatalogEndpoints.Error(409, "REFERENCE_CHANGED"); }
    }
}
