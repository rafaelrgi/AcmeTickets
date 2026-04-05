using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Context.Propagation;
using AcmeTickets.Api.Endpoints;
using AcmeTickets.Api.Hubs;
using AcmeTickets.Domain.Interfaces;
using AcmeTickets.Infra.Repositories;
using AcmeTickets.Application.Interfaces;
using AcmeTickets.Infra.Caching;
using AcmeTickets.Api.Infra;
using AcmeTickets.Domain.Settings;
using AcmeTickets.Infra.Settings;
using Serilog;
using Serilog.Enrichers.Span;
using AcmeTickets.Api.Workers;
using AcmeTickets.Application.Services;
using AcmeTickets.Domain.Common;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("TicketApi"))
            .SetSampler(new AlwaysOnSampler())
            .AddAspNetCoreInstrumentation()
            .AddSource("TicketBooking.Telemetry")
            .AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317")))
        .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("TicketBooking.Metrics")
                .AddPrometheusExporter()
            //.AddOtlpExporter() //Aspire Dashboard
            //.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317")) //Jaeger Dashboard
        );

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithSpan()
#if DEBUG
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}")
#endif
        .WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = "http://localhost:4317";
            //options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc; //Jaeger
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "TicketBooking-API"
            };
        })
    );

    builder.Services.AddSettings(builder.Configuration);
    var settingsUrls = builder.Configuration.GetSection(SettingsUrls.SectionName).Get<SettingsUrls>()!;

    builder.Services.AddAuth(builder.Configuration);

// Redis setup
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? settingsUrls.Redis;
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "TicketBooking_";
    });

    builder.Services.AddAws(builder.Configuration, builder.Environment);

    builder.Services.AddHostedService<TicketUpdateWorker>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    // Cache
    builder.Services.AddSingleton<ITicketCache, TicketCacheService>();
    builder.Services.AddSingleton<IEventCache, EventCacheService>();
    // Repositories
    builder.Services.AddScoped<IEventRepository, DynamoDbEventRepository>();
    builder.Services.AddScoped<ITicketRepository, DynamoDbTicketRepository>();
    // Application
    builder.Services.AddScoped<IEventAppService, EventAppService>();
    builder.Services.AddScoped<ITicketAppService, TicketAppService>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddSignalR();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (settingsUrls.AllowedOrigins != null)
                policy.WithOrigins(settingsUrls.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
        });
    });

    Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

    builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opt =>
    {
        opt.SerializerOptions.Converters.Clear();
        foreach (var converter in JsonDefaults.Options.Converters)
            opt.SerializerOptions.Converters.Add(converter);
        opt.SerializerOptions.PropertyNameCaseInsensitive = JsonDefaults.Options.PropertyNameCaseInsensitive;
    });

    var app = builder.Build();

// Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
        app.UseHttpsRedirection();

    app.UseRouting();
    app.UseCors();
// auth
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseWebSockets();
    app.MapHub<TicketHub>(settingsUrls.TicketHub);

    app.MapEventEndpoints();
    app.MapTicketEndpoints();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Something went terribly wrong starting Api!");
}
finally
{
    Log.CloseAndFlush();
}

// Needed for tests
public partial class Program
{
}
