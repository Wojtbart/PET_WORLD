using MediatR;
using PetWorld.Domain.Entities;

namespace PetWorld.Application.UseCases.GetHistory;

public record GetHistoryQuery() : IRequest<List<ChatMessage>>;
