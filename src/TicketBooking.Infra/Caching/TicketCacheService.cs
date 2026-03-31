using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TicketBooking.Application.Interfaces;

namespace TicketBooking.Infra.Caching;

public class TicketCacheService : BaseCacheService, ITicketCache
{
    protected override string Prefix => "tickets:";

    public TicketCacheService(IDistributedCache cache, ILogger<TicketCacheService> logger) : base(cache, logger)
    {
    }
}
