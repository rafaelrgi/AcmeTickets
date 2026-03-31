namespace TicketBooking.Application.Dtos;

public enum BusMessageType
{
    Ticket,
    Event
}

public record BusMessageDto(
    BusMessageType Message,
    string Event,
    string Ticket = "",
    string Status = ""
);
