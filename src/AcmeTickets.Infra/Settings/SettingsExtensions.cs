using AcmeTickets.Domain.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AcmeTickets.Infra.Settings;

public static class SettingsExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SettingsUrls>(config.GetSection(SettingsUrls.SectionName));
        services.Configure<SettingsAws>(config.GetSection(SettingsAws.SectionName));
        services.Configure<SettingsAuth>(config.GetSection(SettingsAuth.SectionName));
        return services;
    }
}
