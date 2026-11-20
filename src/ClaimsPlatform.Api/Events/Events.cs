using System.Threading.Channels;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimsPlatform.Api.Events;

public static class EventTypes
{
    public const string ClaimSubmitted = "ClaimSubmitted";
    public const string ClaimPickedUp = "ClaimPickedUp";
    public const string InfoRequested = "InfoRequested";
    public const string InfoProvided = "InfoProvided";
    public const string ClaimApproved = "ClaimApproved";
    public const string ClaimRejected = "ClaimRejected";
    public const string ClaimSettled = "ClaimSettled";
}

// The event contract. Flat and JSON-friendly so it can go straight onto a Kafka topic.
public record ClaimDomainEvent(Guid EventId, string Type, Guid ClaimId, string ClaimNumber,
    Guid ClaimantId, Guid? OfficerId, string Detail, DateTime OccurredAt)
{
    public static ClaimDomainEvent From(string type, Claim c, string detail) =>
        new(Guid.NewGuid(), type, c.Id, c.ClaimNumber, c.ClaimantId, c.AssignedOfficerId, detail, DateTime.UtcNow);
}

public interface IEventPublisher
{
    ValueTask PublishAsync(ClaimDomainEvent evt, CancellationToken ct = default);
}

// Default transport: an in-memory queue. Same contract as Kafka, zero infrastructure.
public class InProcessEventBus : IEventPublisher
{
    private readonly Channel<ClaimDomainEvent> _channel = Channel.CreateUnbounded<ClaimDomainEvent>();
    public ChannelReader<ClaimDomainEvent> Reader => _channel.Reader;
    public ValueTask PublishAsync(ClaimDomainEvent evt, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(evt, ct);
}

// Background consumer: runs OUTSIDE the request, so the user never waits for side effects.
public class InProcessEventConsumer(InProcessEventBus bus, IServiceScopeFactory scopes,
    ILogger<InProcessEventConsumer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in bus.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<NotificationHandler>()
                        .HandleAsync(evt, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Handler failed for {Type}", evt.Type);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}

// Turns events into in-app notifications. Idempotent: safe if Kafka delivers an event twice.
public class NotificationHandler(ClaimsDbContext db)
{
    public async Task HandleAsync(ClaimDomainEvent evt, CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(n => n.SourceEventId == evt.EventId, ct)) return;

        var target = evt.Type == EventTypes.InfoProvided ? evt.OfficerId : evt.ClaimantId;
        if (target is null) return;

        string? message = evt.Type switch
        {
            EventTypes.ClaimSubmitted => $"We received your claim {evt.ClaimNumber}.",
            EventTypes.ClaimPickedUp  => $"Your claim {evt.ClaimNumber} is now under review.",
            EventTypes.InfoRequested  => $"Action needed on claim {evt.ClaimNumber}: {evt.Detail}",
            EventTypes.InfoProvided   => $"The claimant responded on claim {evt.ClaimNumber}.",
            EventTypes.ClaimApproved  => $"Good news: claim {evt.ClaimNumber} was approved. {evt.Detail}",
            EventTypes.ClaimRejected  => $"Claim {evt.ClaimNumber} was not approved. {evt.Detail}",
            EventTypes.ClaimSettled   => $"Claim {evt.ClaimNumber} has been settled.",
            _ => null
        };
        if (message is null) return;

        db.Notifications.Add(new Notification
        {
            UserId = target.Value, ClaimId = evt.ClaimId, Message = message, SourceEventId = evt.EventId
        });
        await db.SaveChangesAsync(ct);
    }
}