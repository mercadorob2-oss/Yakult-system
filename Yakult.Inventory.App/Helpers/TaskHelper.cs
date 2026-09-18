using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Provides safe fire-and-forget helpers for tasks that are intentionally
    /// started without awaiting, ensuring exceptions are observed and logged
    /// rather than swallowed silently.
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>
        /// Fires the task without awaiting it. If the task faults, the
        /// <paramref name="onError"/> callback is invoked and the exception
        /// is written to the debug output. This prevents unobserved task
        /// exceptions from disappearing silently.
        /// </summary>
        /// <param name="task">The task to fire.</param>
        /// <param name="onError">Optional handler called on the captured exception.</param>
        public static void FireAndForget(this Task task, Action<Exception> onError = null)
        {
            if (task == null)
                return;

            task.ContinueWith(t =>
            {
                if (!t.IsFaulted)
                    return;

                var ex = t.Exception?.InnerException ?? t.Exception;
                if (ex == null)
                    return;

                onError?.Invoke(ex);
                System.Diagnostics.Debug.WriteLine($"[FireAndForget] {ex}");
            }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Current);
        }
    }
}
