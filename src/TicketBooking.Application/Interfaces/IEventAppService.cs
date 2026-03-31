using TicketBooking.Application.Dtos;
using TicketBooking.Domain.Common;
using TicketBooking.Domain.Entities;

namespace TicketBooking.Application.Interfaces;

public interface IEventAppService
{
    public Task<Result> SaveEvent(CreateEventRequest request);
    public Task<EventStats?> GetStats(string eventId);
    public Task<Event?> GetEvent(string eventId);
    public Task<List<string>?> GetEventIds();
}
