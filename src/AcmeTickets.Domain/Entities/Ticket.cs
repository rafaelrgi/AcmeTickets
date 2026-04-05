using System.Text.Json.Serialization;
using AcmeTickets.Domain.Common;

namespace AcmeTickets.Domain.Entities;

public enum TicketStatus
{
    Reserved,
    Confirmed,
    Available
}

public class Ticket
{
    public string EventId { get; init; }
    public int TicketId { get; set; }
    public string? UserId { get; set; }
    public TicketStatus Status { get; set; }
    public bool IsVip { get; init; }
    public DateTime UpdatedAt { get; set; }

    public static Result<Ticket> Create(string eventId, int ticketId, string? userId, bool isVip = false,
        TicketStatus status = TicketStatus.Available, DateTime updatedAt = default)
    {
        if (string.IsNullOrEmpty(eventId))
            return Result<Ticket>.Fail("Event should an Id (name of the event)");
        if (ticketId <= 0)
            return Result<Ticket>.Fail("Ticket should an Id (number of the ticket/seat)");
        if (string.IsNullOrWhiteSpace(userId) && status != TicketStatus.Available)
            return Result<Ticket>.Fail("Ticket should belong to an User when not Available");

        return Result<Ticket>.Ok(new Ticket(eventId, ticketId, userId, isVip, status, updatedAt));
    }

    public Result Reserve(string userId)
    {
        if (Status != TicketStatus.Available)
            return Result.Fail("Ticket is not Available");
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Fail("Ticket should belong to an User when Reserved");
        UserId = userId.Trim();
        Status = TicketStatus.Reserved;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok();
    }

    public Result Confirm()
    {
        if (Status != TicketStatus.Reserved)
            return Result.Fail("Ticket is not reserved");
        Status = TicketStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
        return Result.Ok();
    }

    [JsonConstructor] //ugly but fast as hell
    private Ticket(string eventId, int ticketId, string? userId, bool isVip, TicketStatus status, DateTime updatedAt)
    {
        EventId = eventId;
        TicketId = ticketId;
        UserId = userId;
        Status = status;
        IsVip = isVip;
        UpdatedAt = updatedAt == default ? DateTime.UtcNow : updatedAt;
    }
}
