using System.Text.Json;
using AcmeTickets.Application.Dtos;
using AcmeTickets.Application.Interfaces;
using AcmeTickets.Domain.Common;
using AcmeTickets.Domain.Entities;
using AcmeTickets.Domain.Interfaces;

namespace AcmeTickets.Application.Services;

public class TicketAppService : ITicketAppService
{
    private readonly ITicketRepository _repository;
    private readonly ITicketCache _cache;
    private readonly IServiceBus _serviceBus;
    private readonly IWorkflows _workflows;

    public TicketAppService(ITicketRepository repository, ITicketCache cache, IServiceBus serviceBus, IWorkflows workflows)
    {
        _repository = repository;
        _cache = cache;
        _serviceBus = serviceBus;
        _workflows = workflows;
    }

    public async Task<List<Ticket>> GetTickets(string eventId)
    {
        // cache
        var cachedData = await _cache.Get(eventId);
        if (!string.IsNullOrEmpty(cachedData))
            return JsonSerializer.Deserialize<List<Ticket>>(cachedData) ?? [];

        // db
        //logger.LogDebug("CacheMiss: GetTickets {eventId}", eventId);
        var tickets = await _repository.GetTickets(eventId);
        if (tickets.Count > 0)
            await _cache.Set(eventId, JsonSerializer.Serialize(tickets));
        return tickets;
    }

    public async Task<Result> ConfirmTicket(TicketConfirmationRequest request)
    {
        var ticket = await _repository.GetTicket(request.EventId, request.TicketId);
        if (ticket == null)
            return Result.Fail("Ticket not found");
        var result = ticket.Confirm();
        if (!result.IsSuccess)
            return result;

        if (!await _repository.ConfirmTicket(ticket))
            return Result.Fail("Error saving ticket ");

        await Task.WhenAll
        (
            _cache.Invalidate(request.EventId),
            NotifyQueue(ticket, TicketStatus.Confirmed)
        );
        return Result.Ok();
    }

    public async Task<Result> ReserveTicket(TicketReservationRequest request, IEventRepository eventRepository)
    {
        var evt = await eventRepository.GetEvent(request.EventId);
        if (evt is null)
            return Result.Fail("Event invalid");
        if (request.TicketId >= evt.TotalTickets)
            return Result.Fail("Ticket invalid (over quota)");

        var ticket = Ticket.Create(
            request.EventId,
            request.TicketId,
            request.UserId,
            request.IsVip,
            TicketStatus.Reserved
        );
        var success = await _repository.ReserveTicket(ticket.Value);
        await _cache.Invalidate(request.EventId);
        if (success)
            await Task.WhenAll
            (
                NotifyQueue(ticket.Value, TicketStatus.Reserved),
                _workflows.StartReservationFlow(ticket.Value)
            );

        return success
            ? Result.Ok()
            : Result.Fail("Could not reserve the ticket, is it available?");
    }

    private async Task NotifyQueue(Ticket ticket, TicketStatus status)
    {
        var message = new BusMessageDto
        (
            BusMessageType.Ticket,
            ticket.EventId,
            ticket.TicketId.ToString(),
            status.ToString()
        );
        await _serviceBus.Publish(message);
        //logger.LogDebug("NotifyQueue {ticket.EventId}.{ticket.TicketId} = {status}", ticket.EventId, ticket.TicketId, status);
    }
}
