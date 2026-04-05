namespace AcmeTickets.Application.Dtos;

public record TicketReservationRequest(string EventId, int TicketId, bool IsVip, string UserId);

public record TicketConfirmationRequest(string EventId, int TicketId);
