using System.Text.Json;
using TicketBooking.Application.Dtos;
using TicketBooking.Application.Interfaces;
using TicketBooking.Domain.Common;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Interfaces;

namespace TicketBooking.Application.Services;

public class EventAppService : IEventAppService
{
    private readonly IEventRepository _repository;
    private readonly IEventCache _cache;
    private readonly IServiceBus _serviceBus;

    public EventAppService(IEventRepository repository, IEventCache cache, IServiceBus serviceBus)
    {
        _repository = repository;
        _cache = cache;
        _serviceBus = serviceBus;
    }

    public async Task<Result> SaveEvent(CreateEventRequest request)
    {
        var evt = Event.Create(request.EventId, request.TotalTickets);
        if (!evt.IsSuccess)
            return Result.Fail(evt.ErrorMessage ?? "Failed to save event");

        var success = await _repository.CreateEvent(evt.Value);
        await _cache.Invalidate("EventIds");
        if (!success)
            return Result.Fail("Failed to save event");

        var message = new BusMessageDto
        (
            BusMessageType.Event,
            evt.Value.EventId
        );
        await _serviceBus.Publish(message);
        //logger.LogDebug("NotifyQueue {row.EventId}.{row.TotalTickets}", row.EventId, row.TotalTickets);

        return Result.Ok();
    }

    public async Task<EventStats?> GetStats(string eventId)
    {
        var stats = await _repository.GetDashboardStats(eventId);
        return stats;
    }

    public async Task<Event?> GetEvent(string eventId)
    {
        var row = await _repository.GetEvent(eventId);
        return row;
    }

    public async Task<List<string>?> GetEventIds()
    {
        // cache
        var cachedData = await _cache.Get("EventIds");
        if (!string.IsNullOrEmpty(cachedData))
            return JsonSerializer.Deserialize<List<string>>(cachedData);
        // db
        var eventIds = await _repository.GetEventIds();
        await _cache.Set("EventIds", JsonSerializer.Serialize(eventIds));
        return eventIds;
    }
}
