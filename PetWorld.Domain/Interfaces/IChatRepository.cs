using PetWorld.Domain.Entities;

namespace PetWorld.Domain.Interfaces;

public interface IChatRepository
{
    Task SaveAsync(ChatMessage message, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetAllAsync(CancellationToken cancellationToken = default);
}
