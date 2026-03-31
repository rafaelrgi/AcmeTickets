using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TicketBooking.Application.Interfaces;

namespace TicketBooking.Infra.Caching;

public class EventCacheService: BaseCacheService, IEventCache
{
    protected override string Prefix => "events:";

    public EventCacheService(IDistributedCache cache, ILogger<TicketCacheService> logger) : base(cache, logger)
    {
    }
}
