using AcmeTickets.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AcmeTickets.Infra.Caching;

public class EventCacheService: BaseCacheService, IEventCache
{
    protected override string Prefix => "events:";

    public EventCacheService(IDistributedCache cache, ILogger<TicketCacheService> logger) : base(cache, logger)
    {
    }
}
