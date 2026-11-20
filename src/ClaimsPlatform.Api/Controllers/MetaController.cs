using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class MetaController(ClaimsDbContext db, UserContext users) : ControllerBase
{
    // Demo-only: lists seeded users so the UI and Swagger can "sign in".
    [HttpGet("users")]
    public async Task<List<UserDto>> Users(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role)).ToListAsync(ct);

    [HttpGet("notifications")]
    public async Task<List<NotificationDto>> Notifications(CancellationToken ct)
    {
        var user = await users.RequireAsync();
        var rows = await db.Notifications.AsNoTracking().Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt).Take(20).ToListAsync(ct);
        return rows.Select(n => new NotificationDto(n.Id, n.ClaimId, n.Message, ClaimMapper.Utc(n.CreatedAt))).ToList();
    }
}