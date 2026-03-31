using System.Text.Json;
using TicketBooking.Domain.Interfaces;
using TicketBooking.Application.Dtos;
using TicketBooking.Application.Interfaces;
using TicketBooking.Domain.Constants;

namespace TicketBooking.Api.Endpoints;

public static class EventApi
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Events.GetEvents, GetEventIds).RequireAuthorization();
        app.MapGet($"{ApiRoutes.Events.GetEvent}{{eventId}}", GetEvent).RequireAuthorization();
        app.MapGet($"{ApiRoutes.Events.GetStats}{{eventId}}", GetStats).RequireAuthorization();

        app.MapPut(ApiRoutes.Events.SaveEvent, SaveEvent).RequireAuthorization(AuthConstants.AdminPolicy);

        return app;
    }

    private static async Task<IResult> GetEvent(string eventId, IEventAppService eventService)
    {
        // var logger = loggerFactory.CreateLogger("EventApi");
        // logger.LogDebug(">>> GetEvent: {eventId}", eventId);
        var row = await eventService.GetEvent(eventId);
        return (row == null) ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> SaveEvent(CreateEventRequest request, IEventAppService eventService)
    {
        //var logger = loggerFactory.CreateLogger("EventApi");
        //logger.LogDebug(">>> SaveEvent: {event}", request);
        var result = await eventService.SaveEvent(request);
        return ! result.IsSuccess ? Results.BadRequest(result.ErrorMessage) : Results.NoContent();
    }

    private static async Task<IResult> GetEventIds(IEventAppService eventService)
    {
        //var logger = loggerFactory.CreateLogger("EventApi");
        var eventIds = await eventService.GetEventIds();
        return eventIds == null ? Results.NotFound() : Results.Ok(eventIds);
    }

    private static async Task<IResult> GetStats(string eventId, IEventAppService eventService)
    {
        //var logger = loggerFactory.CreateLogger("EventApi");
        var stats = await eventService.GetStats(eventId);
        return stats == null ? Results.NotFound() : Results.Ok(stats);
    }
}
