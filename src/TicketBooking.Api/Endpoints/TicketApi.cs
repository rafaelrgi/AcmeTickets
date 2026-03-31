using System.Text.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Microsoft.Extensions.Options;
using TicketBooking.Application.Dtos;
using TicketBooking.Domain.Entities;
using TicketBooking.Domain.Interfaces;
using TicketBooking.Application.Interfaces;
using TicketBooking.Domain.Common;
using TicketBooking.Domain.Constants;
using TicketBooking.Domain.Exceptions;
using TicketBooking.Domain.Settings;

namespace TicketBooking.Api.Endpoints;

public static class TicketApi
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Tickets.ReserveTicket, ReserveTicket).RequireAuthorization();
        app.MapPost(ApiRoutes.Tickets.ConfirmTicket, ConfirmTicket).RequireAuthorization();

        app.MapGet($"{ApiRoutes.Tickets.GetTickets}{{eventId}}", GetTickets).RequireAuthorization(AuthConstants.AdminPolicy);

        return app;
    }

    private static async Task<IResult> GetTickets(string eventId, ITicketAppService ticketService) //HttpContext context
    {
        //var logger = loggerFactory.CreateLogger("TicketApi");
        //logger.LogDebug("GetTickets: {eventId}", eventId);
        //logger.LogDebug("GetTickets: {eventId} :: Token: {authHeader}", eventId, context.Request.Headers["Authorization"]);
        var tickets = await ticketService.GetTickets(eventId);
        return Results.Ok(tickets);
    }

    private static async Task<IResult> ConfirmTicket(TicketConfirmationRequest request,
        ITicketAppService ticketService, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("TicketApi");
        try
        {
            var result = await ticketService.ConfirmTicket(request);
            return !result.IsSuccess ? Results.BadRequest(result.ErrorMessage) : Results.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "ConfirmTicket error");
            return Results.InternalServerError();
        }
    }

    private static async Task<IResult> ReserveTicket(TicketReservationRequest request, ITicketAppService ticketService,
        IEventRepository eventRepository, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("TicketApi");
        try
        {
            var result = await ticketService.ReserveTicket(request, eventRepository);
            return !result.IsSuccess ? Results.BadRequest(result.ErrorMessage) : Results.Ok();
        }
        catch (Exception e)
        {
            logger.LogError(e, "ReserveTicket error");
            return Results.InternalServerError();
        }
    }
}
