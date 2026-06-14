namespace RushOrder.Application.Analytics.DTOs;

public sealed record SalesDto(
    IReadOnlyList<SalesSeriesPoint> Series,
    SalesTotals Totals);

public sealed record SalesSeriesPoint(
    DateTimeOffset Date,
    decimal Revenue,
    int Orders,
    int Covers);

public sealed record SalesTotals(
    decimal Revenue,
    int Orders,
    decimal AvgTicket,
    DateTimeOffset? BestDay,
    DateTimeOffset? WorstDay);
