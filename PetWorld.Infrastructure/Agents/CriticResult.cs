using System.Text.Json.Serialization;

namespace PetWorld.Infrastructure.Agents;

internal record CriticResult(
    [property: JsonPropertyName("approved")] bool Approved,
    [property: JsonPropertyName("feedback")] string Feedback
);
