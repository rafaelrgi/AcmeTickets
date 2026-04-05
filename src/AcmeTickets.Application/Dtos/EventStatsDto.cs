namespace AcmeTickets.Application.Dtos;

public record EventStatsDto(
    string Event,
    int Total,
    int Confirmed,
    int Reserved,
    int Available
);
