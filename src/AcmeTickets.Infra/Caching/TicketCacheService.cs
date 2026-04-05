using AcmeTickets.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace AcmeTickets.Infra.Caching;

public class TicketCacheService : BaseCacheService, ITicketCache
{
    protected override string Prefix => "tickets:";

    public TicketCacheService(IDistributedCache cache, ILogger<TicketCacheService> logger) : base(cache, logger)
    {
    }
}
