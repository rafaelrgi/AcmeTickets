using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TicketBooking.Application.Interfaces;

namespace TicketBooking.Infra.Caching;

public abstract class BaseCacheService : ICacheService
{
    protected abstract string Prefix { get; }
    protected readonly IDistributedCache Cache;
    protected readonly ILogger<ICacheService> Logger;
    protected int Seconds => 300;

    protected BaseCacheService(IDistributedCache cache, ILogger<ICacheService> logger)
    {
        Cache = cache;
        Logger = logger;
    }

    public async Task Invalidate(string key)
    {
        try
        {
            Logger.LogDebug("Clear cache {Prefix}{key}", Prefix, key);
            await Cache.RemoveAsync($"{Prefix}{key}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Redis Offline: {msg}", ex.Message);
        }
    }

    public async Task<string?> Get(string key)
    {
        try
        {
            return await Cache.GetStringAsync($"{Prefix}{key}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Redis Offline (Read): {msg}", ex.Message);
            return null;
        }
    }

    public async Task Set(string key, string data)
    {
        try
        {
            Logger.LogDebug("Set cache {Prefix}{key}", Prefix, key);
            await Cache.SetStringAsync($"{Prefix}{key}", data,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Seconds) });
        }
        catch (Exception ex)
        {
            Logger.LogError("Redis Offline: {msg}", ex.Message);
        }
    }
}