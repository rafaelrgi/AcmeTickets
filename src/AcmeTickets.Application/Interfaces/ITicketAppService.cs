using AcmeTickets.Application.Dtos;
using AcmeTickets.Domain.Common;
using AcmeTickets.Domain.Entities;
using AcmeTickets.Domain.Interfaces;

namespace AcmeTickets.Application.Interfaces;

public interface ITicketAppService
{
    public Task<List<Ticket>> GetTickets(string eventId);

    public Task<Result> ConfirmTicket(TicketConfirmationRequest request);

    public Task<Result> ReserveTicket(TicketReservationRequest request, IEventRepository eventRepository);
}
