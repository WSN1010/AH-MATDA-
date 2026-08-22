using Microsoft.Extensions.Hosting;

namespace Ajure.Infrastructure;

public sealed class AjureStoreInitializer(AjureStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        store.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
