using Moq;
using PetWorld.Application.UseCases.SendMessage;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Tests;

public class SendMessageHandlerTests
{
    private readonly Mock<IAgentService> _agentMock = new();
    private readonly Mock<IChatRepository> _repoMock = new();

    [Fact]
    public async Task Handle_SavesChatMessage_AndReturnsAgentResult()
    {
        _agentMock
            .Setup(x => x.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentResult("Polecamy Royal Canin 15kg za 289 zł.", 2));

        ChatMessage? saved = null;
        _repoMock
            .Setup(x => x.SaveAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatMessage, CancellationToken>((msg, _) => saved = msg)
            .Returns(Task.CompletedTask);

        var handler = new SendMessageHandler(_agentMock.Object, _repoMock.Object);
        var result = await handler.Handle(new SendMessageCommand("Jaka karma dla psa?"), CancellationToken.None);

        Assert.Equal("Polecamy Royal Canin 15kg za 289 zł.", result.Answer);
        Assert.Equal(2, result.Iterations);
        Assert.NotNull(saved);
        Assert.Equal("Jaka karma dla psa?", saved!.Question);
        Assert.Equal(2, saved.Iterations);
    }
}
