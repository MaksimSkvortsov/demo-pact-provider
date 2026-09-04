using System.Text.Json;

namespace DemoPactProvider.Api.Models;

public sealed record ProviderStateRequest(
    string? State,
    JsonElement? Params);
