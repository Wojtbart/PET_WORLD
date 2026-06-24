using Microsoft.EntityFrameworkCore;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Infrastructure.Persistence;

public class ChatRepository : IChatRepository
{
    private readonly AppDbContext _context;

    public ChatRepository(AppDbContext context) => _context = context;

    public async Task SaveAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<ChatMessage>> GetAllAsync(CancellationToken cancellationToken = default)
        => _context.ChatMessages
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
}
