using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Infrastructure;

// Turns exceptions into consistent JSON error responses with the right status code.
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                InvalidInputException => (400, "Invalid input"),
                UnauthorizedException => (401, "Unauthorized"),
                ForbiddenException => (403, "Forbidden"),
                NotFoundException => (404, "Not found"),
                DomainException => (409, "Business rule violated"),
                DbUpdateConcurrencyException => (409, "Concurrent update"),
                _ => (500, "Unexpected error")
            };

            var detail = ex.Message;
            if (status == 500) { log.LogError(ex, "Unhandled exception"); detail = "An unexpected error occurred."; }
            if (ex is DbUpdateConcurrencyException) detail = "The claim was changed by someone else. Reload and try again.";

            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new { status, title, detail });
        }
    }
}

// DEMO-ONLY auth: the caller identifies itself with an X-User-Id header.
// Roles are still enforced server-side. Production: OIDC/JWT, same RequireAsync call sites.
public class UserContext(ClaimsDbContext db, IHttpContextAccessor accessor)
{
    public async Task<User> RequireAsync(params UserRole[] allowed)
    {
        var header = accessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
        if (!Guid.TryParse(header, out var id))
            throw new UnauthorizedException("Missing or invalid X-User-Id header.");

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new UnauthorizedException("Unknown user.");

        if (allowed.Length > 0 && !allowed.Contains(user.Role))
            throw new ForbiddenException($"This action requires role: {string.Join(" or ", allowed)}.");
        return user;
    }
}