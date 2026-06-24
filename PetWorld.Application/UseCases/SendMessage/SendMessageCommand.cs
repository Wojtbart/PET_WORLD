using MediatR;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.SendMessage;

public record SendMessageCommand(string Question) : IRequest<AgentResult>;
