using System.Text.Json.Serialization;
using TicketBooking.Domain.Common;
using TicketBooking.Domain.Exceptions;

namespace TicketBooking.Domain.Entities;

public sealed class Event
{
    public string EventId { get; init; }
    public int TotalTickets { get; init; }

    public static Result<Event> Create(string eventId, int totalTickets)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            return Result<Event>.Fail("Event should an Id (name of the event)");
        if (totalTickets <= 0)
            return Result<Event>.Fail("Event should have tickets");
        return Result<Event>.Ok(new Event(eventId, totalTickets));
    }

    [JsonConstructor] //ugly but fast as hell
    private Event(string eventId, int totalTickets)
    {
        EventId = eventId.Trim();
        TotalTickets = totalTickets;
    }

}
