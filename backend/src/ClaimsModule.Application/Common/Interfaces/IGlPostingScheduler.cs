using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IGlPostingScheduler
{
    void Enqueue(ReserveHistory transaction);
}
