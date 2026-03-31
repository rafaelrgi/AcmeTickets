using TicketBooking.Application.Dtos;
using TicketBooking.Domain.Common;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Interfaces;

namespace TicketBooking.Application.Interfaces;

public interface ITicketAppService
{
    public Task<List<Ticket>> GetTickets(string eventId);

    public Task<Result> ConfirmTicket(TicketConfirmationRequest request);

    public Task<Result> ReserveTicket(TicketReservationRequest request, IEventRepository eventRepository);
}
