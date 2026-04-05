using System.Net;
using System.Net.Http.Headers;
using AcmeTickets.Admin.Infra;
using Microsoft.AspNetCore.Authentication;

namespace AcmeTickets.Admin;

public class AuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthEventsService _authEvents;
    private readonly ILogger<AuthHandler> _logger;

    public AuthHandler(IHttpContextAccessor httpContextAccessor, AuthEventsService authEvents, ILogger<AuthHandler> logger)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _authEvents = authEvents;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var accessToken = await httpContext?.GetTokenAsync("access_token")!;
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else
        {
            _logger.LogError("AuthHandler missing access_token: {request}", request.RequestUri);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _authEvents.NotifyUnauthorized();
        }
        return response;
    }
}
