using MediatR;

namespace ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;

public record DetectSlaBreachesCommand : IRequest<int>;
