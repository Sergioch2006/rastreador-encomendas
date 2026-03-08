using MediatR;

namespace GlobalLogistics.Application.Commands;

public record UpdatePackageLocationCommand(
    string TrackingCode,
    int Status,
    string Location,
    double Latitude,
    double Longitude,
    string Description
) : IRequest<Guid>;

public class UpdatePackageLocationHandler : IRequestHandler<UpdatePackageLocationCommand, Guid>
{
    private readonly IServiceProvider _serviceProvider;

    public UpdatePackageLocationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Guid> Handle(UpdatePackageLocationCommand request, CancellationToken cancellationToken)
    {
        // Retorna um ID de correlação; a persistência real ocorre via mensageria
        var correlationId = Guid.NewGuid();
        return await Task.FromResult(correlationId);
    }
}
