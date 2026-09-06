namespace DemoPactProvider.Api.Models;

public sealed record CustomerResponse(
    int Id,
    string Email,
    string Phone);
