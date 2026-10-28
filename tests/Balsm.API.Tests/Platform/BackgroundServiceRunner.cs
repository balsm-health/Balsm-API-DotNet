using Microsoft.Extensions.Hosting;

namespace Balsm.API.Tests.Platform;

/// <summary>
/// Starts a hosted loop, waits until it reaches a state the test can observe,
/// and stops it again. The loops sleep until their next cron occurrence, so a
/// test only ever sees the pass up to that sleep.
/// </summary>
internal static class BackgroundServiceRunner
{
    public static async Task RunUntilAsync(BackgroundService service, Func<bool> reached)
    {
        await service.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!reached() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
