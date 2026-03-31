using TicketBooking.Domain.Entities;

namespace TicketBooking.Domain.Interfaces;

public interface IWorkflows
{
    public Task<object> StartReservationFlow(Ticket ticket);
}
