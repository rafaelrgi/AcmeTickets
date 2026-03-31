namespace TicketBooking.Application.Interfaces;

public interface ICacheService
{
    public Task<string?> Get(string key);
    public Task Set(string key, string data);
    public Task Invalidate(string key);
}
