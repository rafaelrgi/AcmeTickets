using AcmeTickets.Domain.Entities;

namespace AcmeTickets.Domain.Interfaces;

public interface IWorkflows
{
    public Task<object> StartReservationFlow(Ticket ticket);
}
