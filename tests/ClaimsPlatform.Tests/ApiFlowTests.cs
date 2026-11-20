using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsPlatform.Api.Contracts;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ClaimsPlatform.Tests;

public class ApiFlowTests(ApiFlowTests.Factory factory) : IClassFixture<ApiFlowTests.Factory>
{
    // Each test run gets its own throwaway SQLite file and no sample claims.
    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"claims-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
            builder.UseSetting("Seed:SampleClaims", "false");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { File.Delete(_dbPath); } catch { /* file may still be locked on Windows */ }
        }
    }

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    private async Task<ClaimDetailDto> SubmitAsync(decimal amount = 5000m)
    {
        var res = await ClientFor(SeedData.Aisha).PostAsJsonAsync("/api/claims",
            new SubmitClaimRequest(ClaimType.Motor, Market.Malaysia, DateOnly.FromDateTime(DateTime.UtcNow),
                "Rear-ended at traffic lights", amount), Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ClaimDetailDto>(Json))!;
    }

    private static async Task<ClaimDetailDto> ReadAsync(HttpResponseMessage res)
    {
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<ClaimDetailDto>(Json))!;
    }

    [Fact]
    public async Task Full_lifecycle_submit_review_info_approve_settle_with_notifications()
    {
        var claimant = ClientFor(SeedData.Aisha);
        var officer = ClientFor(SeedData.Raj);
        var manager = ClientFor(SeedData.Grace);

        var claim = await SubmitAsync(4800m);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);

        var queue = await officer.GetFromJsonAsync<List<ClaimSummaryDto>>("/api/officer/queue", Json);
        Assert.Contains(queue!, c => c.Id == claim.Id);

        var d = await ReadAsync(await officer.PostAsync($"/api/officer/claims/{claim.Id}/pickup", null));
        Assert.Equal(ClaimStatus.UnderReview, d.Status);

        d = await ReadAsync(await officer.PostAsJsonAsync($"/api/officer/claims/{claim.Id}/request-info",
            new RequestInfoRequest("Please share the police report number."), Json));
        Assert.Equal(ClaimStatus.InfoRequested, d.Status);

        d = await ReadAsync(await claimant.PostAsJsonAsync(
            $"/api/claims/{claim.Id}/info-requests/{d.InfoRequests.Single().Id}/respond",
            new RespondInfoRequest("Report KL-12345"), Json));
        Assert.Equal(ClaimStatus.UnderReview, d.Status);

        d = await ReadAsync(await officer.PostAsJsonAsync($"/api/officer/claims/{claim.Id}/approve",
            new ApproveRequest(4500m, "Covered under policy"), Json));
        Assert.Equal(ClaimStatus.Approved, d.Status);

        var summary = await manager.GetFromJsonAsync<ManagerSummaryDto>("/api/manager/summary", Json);
        Assert.True(summary!.TotalExposure >= 4500m);

        d = await ReadAsync(await officer.PostAsync($"/api/officer/claims/{claim.Id}/settle", null));
        Assert.Equal(ClaimStatus.Settled, d.Status);
        Assert.Equal(6, d.Timeline.Count);

        // Notifications are created asynchronously by the event consumer: poll briefly.
        List<NotificationDto>? notes = null;
        for (var i = 0; i < 25; i++)
        {
            notes = await claimant.GetFromJsonAsync<List<NotificationDto>>("/api/notifications", Json);
            if (notes!.Count(n => n.ClaimId == claim.Id) >= 5) break;
            await Task.Delay(200);
        }
        var claimNotes = notes!
    .Where(n => n.ClaimId == claim.Id)
    .ToList();

Assert.Equal(5, claimNotes.Count);
Assert.Contains(claimNotes, n => n.Message.Contains("approved"));
    }

    [Fact]
    public async Task Missing_user_is_401_and_wrong_role_is_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/claims/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ClientFor(SeedData.Aisha).GetAsync("/api/officer/queue")).StatusCode);
    }

    [Fact]
    public async Task Ownership_and_state_rules_are_enforced()
    {
        var claim = await SubmitAsync();
        var raj = ClientFor(SeedData.Raj);
        var mei = ClientFor(SeedData.Mei);

        // Another claimant cannot read it.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ClientFor(SeedData.Wei).GetAsync($"/api/claims/{claim.Id}")).StatusCode);

        await raj.PostAsync($"/api/officer/claims/{claim.Id}/pickup", null);

        // Second officer cannot take it or decide it.
        Assert.Equal(HttpStatusCode.Conflict,
            (await mei.PostAsync($"/api/officer/claims/{claim.Id}/pickup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await mei.PostAsJsonAsync($"/api/officer/claims/{claim.Id}/approve",
                new ApproveRequest(100m, "not mine"), Json)).StatusCode);

        // After approval, asking for more info is an invalid transition.
        await raj.PostAsJsonAsync($"/api/officer/claims/{claim.Id}/approve", new ApproveRequest(100m, "ok"), Json);
        Assert.Equal(HttpStatusCode.Conflict,
            (await raj.PostAsJsonAsync($"/api/officer/claims/{claim.Id}/request-info",
                new RequestInfoRequest("too late?"), Json)).StatusCode);
    }
}