namespace AcmeTickets.Admin.Infra;

public class AuthEventsService
{
    public event Action? OnUnauthorizedDetected;

    public void NotifyUnauthorized()
    {
        OnUnauthorizedDetected?.Invoke();
    }
}
