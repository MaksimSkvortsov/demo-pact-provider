namespace DemoPactProvider.Api.Models;

public sealed record CustomerResponse(
    int Id,
    string Name,
    string Email,
    string Phone);
