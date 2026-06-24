using MediatR;
using PetWorld.Domain.Entities;
using PetWorld.Domain.Interfaces;

namespace PetWorld.Application.UseCases.GetHistory;

public class GetHistoryHandler : IRequestHandler<GetHistoryQuery, List<ChatMessage>>
{
    private readonly IChatRepository _repository;

    public GetHistoryHandler(IChatRepository repository) => _repository = repository;

    public Task<List<ChatMessage>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
        => _repository.GetAllAsync(cancellationToken);
}
