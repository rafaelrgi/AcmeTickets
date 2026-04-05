using AcmeTickets.Application.Dtos;
using AcmeTickets.Domain.Common;
using AcmeTickets.Domain.Entities;

namespace AcmeTickets.Application.Interfaces;

public interface IEventAppService
{
    public Task<Result> SaveEvent(CreateEventRequest request);
    public Task<EventStats?> GetStats(string eventId);
    public Task<Event?> GetEvent(string eventId);
    public Task<List<string>?> GetEventIds();
}
