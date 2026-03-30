#pragma warning disable CA1031

namespace GroundControl.Api.Extensions.Threading;

internal static partial class TaskExtensions
{
    public static void FireAndForget(this ValueTask task, ILogger? logger = null, string? taskName = null)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _ = ForgetAwaited(task, taskName, logger);
        }

        static async Task ForgetAwaited(ValueTask task, string? taskName, ILogger? logger)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.TaskExecutionError(ex, taskName);
            }
        }
    }

    public static void FireAndForget<T>(this ValueTask<T> task, ILogger? logger = null, string? taskName = null)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _ = ForgetAwaited(task, taskName, logger);
        }

        static async Task ForgetAwaited(ValueTask<T> task, string? taskName, ILogger? logger)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.TaskExecutionError(ex, taskName);
            }
        }
    }

    public static void FireAndForget(this Task task, string? taskName = null, ILogger? logger = null)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _ = ForgetAwaited(task, taskName, logger);
        }

        static async Task ForgetAwaited(Task task, string? taskName, ILogger? logger)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.TaskExecutionError(ex, taskName);
            }
        }
    }

    [LoggerMessage(0, LogLevel.Error, "An error occurred while executing task: {TaskName}")]
    private static partial void TaskExecutionError(this ILogger logger, Exception exception, string? taskName);
}