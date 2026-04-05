using System.Diagnostics;
using System.Diagnostics.Metrics;
using AcmeTickets.Api.Hubs;
using AcmeTickets.Application.Dtos;
using AcmeTickets.Application.Interfaces;
using AcmeTickets.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AcmeTickets.Api.Workers;

public class TicketUpdateWorker : BackgroundService
{
    private readonly IServiceBus _serviceBus;
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly ITicketCache _ticketCache;
    private readonly IEventCache _eventCache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TicketUpdateWorker> _logger;

    public TicketUpdateWorker(IServiceBus serviceBus, IHubContext<TicketHub> hubContext,
        ITicketCache ticketCache, IEventCache eventCache,
        IServiceScopeFactory scopeFactory, ILogger<TicketUpdateWorker> logger)
    {
        _serviceBus = serviceBus;
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _ticketCache = ticketCache;
        _eventCache = eventCache;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _logger.LogInformation("Starting Worker {queueUrl}", _serviceBus.QueueUrl);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _serviceBus.Subscribe<BusMessageDto>(async (message, cancelToken) =>
        {
            try
            {
                return await ProcessMessage(message, cancelToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Worker Error: {error}", ex.Message);
                return false;
            }
        }, _logger, cancellationToken);
    }

    public async Task<bool> ProcessMessage(BusMessageDto messageDto, CancellationToken cancelToken)
    {
        _logger.LogDebug("Worker Message: {message}", messageDto.ToString());

        try
        {
            var message = CleanMessage(messageDto);
            if (message.Message == BusMessageType.Ticket)
                await _ticketCache.Invalidate(message.Event);
            else
                await _eventCache.Invalidate(message.Event);

            await NotifyHub(message.Message, message.Event, cancelToken);

            if (message.Message == BusMessageType.Ticket)
                NotifyTelemetry(message);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError("Error processing message: {message} :: {error}", messageDto.ToString(), e.Message);
            return false;
            //UNDONE: DLQ
        }
    }

    private void NotifyTelemetry(BusMessageDto message)
    {
        Counter<long> counter = message.Status switch
        {
            "Reserved" => TelemetryConfig.ReservedCounter,
            "Confirmed" => TelemetryConfig.ConfirmedCounter,
            "Available" => TelemetryConfig.CanceledCounter,
            _ => throw new ArgumentOutOfRangeException()
        };
        counter.Add(1, new TagList { { "event.name", message.Event }, { "status", "success" } });
        /*
        TelemetryConfig.TicketMeter.CreateObservableGauge("sqs.queue.size",
            () => GetQueueSizeFromLocalStack()
            "Mensagens",
            "Quantidade de mensagens pendentes na fila");
        */
    }

    private async Task NotifyHub(BusMessageType messageType, string eventId, CancellationToken stoppingToken)
    {
        string message = messageType.ToString();
        _logger.LogDebug("NotifyHub: {message} {eventId}", message, eventId);
        await _hubContext.Clients.All.SendAsync(message, eventId, stoppingToken);
    }

    /// <summary>
    /// Remove EVENT# and TICKET#, it is public static just for tests
    /// </summary>
    public static BusMessageDto CleanMessage(BusMessageDto message)
    {
        return message with
        {
            Event = message.Event.Replace("EVENT#", ""),
            Ticket = message.Ticket.Replace("TICKET#", "")
        };
    }
}
