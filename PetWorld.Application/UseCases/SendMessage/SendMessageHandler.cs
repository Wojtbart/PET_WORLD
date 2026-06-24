using MediatR;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.SendMessage;

public class SendMessageHandler : IRequestHandler<SendMessageCommand, AgentResult>
{
    private readonly IAgentService _agentService;
    private readonly IChatRepository _repository;

    public SendMessageHandler(IAgentService agentService, IChatRepository repository)
    {
        _agentService = agentService;
        _repository = repository;
    }

    public async Task<AgentResult> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var result = await _agentService.GenerateResponseAsync(request.Question, cancellationToken);

        await _repository.SaveAsync(new ChatMessage
        {
            Question = request.Question,
            Answer = result.Answer,
            Iterations = result.Iterations,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return result;
    }
}
