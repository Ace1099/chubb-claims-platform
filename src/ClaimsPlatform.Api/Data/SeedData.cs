using ClaimsPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Data;

public static class SeedData
{
    // Fixed IDs so Swagger, tests and the UI can refer to them.
    public static readonly Guid Aisha = Guid.Parse("11111111-1111-1111-1111-111111111111"); // claimant
    public static readonly Guid Wei   = Guid.Parse("22222222-2222-2222-2222-222222222222"); // claimant
    public static readonly Guid Raj   = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"); // officer
    public static readonly Guid Mei   = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"); // officer
    public static readonly Guid Grace = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"); // manager

    public static async Task InitAsync(ClaimsDbContext db, bool sampleClaims)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        db.Users.AddRange(
            new User { Id = Aisha, Name = "Aisha Rahman", Email = "aisha@example.com", Role = UserRole.Claimant },
            new User { Id = Wei, Name = "Wei Chen", Email = "wei@example.com", Role = UserRole.Claimant },
            new User { Id = Raj, Name = "Raj Kumar", Email = "raj@chubb.example", Role = UserRole.Officer },
            new User { Id = Mei, Name = "Mei Tan", Email = "mei@chubb.example", Role = UserRole.Officer },
            new User { Id = Grace, Name = "Grace Lim", Email = "grace@chubb.example", Role = UserRole.Manager });
        await db.SaveChangesAsync();

        if (!sampleClaims) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var claims = new List<Claim>
        {
            Claim.Submit(Aisha, ClaimType.Motor, Market.Malaysia, today.AddDays(-6), "Rear-ended at traffic lights", 5000m),
            Claim.Submit(Wei, ClaimType.Property, Market.Singapore, today.AddDays(-4), "Burst pipe flooded the kitchen", 22000m),
            Claim.Submit(Aisha, ClaimType.Motor, Market.Thailand, today.AddDays(-3), "Windscreen cracked by road debris", 900m),
            Claim.Submit(Wei, ClaimType.Property, Market.Indonesia, today.AddDays(-9), "Storm damage to roof", 15000m),
            Claim.Submit(Aisha, ClaimType.Motor, Market.HongKong, today.AddDays(-12), "Parking lot scrape", 1800m),
            Claim.Submit(Wei, ClaimType.Property, Market.Philippines, today.AddDays(-20), "Theft of electronics", 9000m),
        };
        // claims[0] stays Submitted so the officer queue is not empty.
        claims[1].PickUp(Raj);
        claims[2].PickUp(Raj);
        claims[2].RequestInfo(Raj, "Please share a photo of the damage and the police report number.");
        claims[3].PickUp(Mei);
        claims[3].Approve(Mei, 12000m, "Covered under the home policy.");
        claims[4].PickUp(Mei);
        claims[4].Reject(Mei, "Damage pre-dates the policy start date.");
        claims[5].PickUp(Raj);
        claims[5].Approve(Raj, 8000m, "Covered; excess applied.");
        claims[5].Settle(Raj);

        db.Claims.AddRange(claims);
        await db.SaveChangesAsync();
    }
}