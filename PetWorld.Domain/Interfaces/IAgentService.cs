namespace PetWorld.Domain.Interfaces;

public record AgentResult(string Answer, int Iterations);

public interface IAgentService
{
    Task<AgentResult> GenerateResponseAsync(string question, CancellationToken cancellationToken = default);
}
