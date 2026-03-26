using System.Threading;
using System.Threading.Tasks;

namespace BlueBattery.Services.State;

public interface IAppStateStore
{
    Task<AppStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppStateSnapshot snapshot, CancellationToken cancellationToken = default);
}
