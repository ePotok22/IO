using System;
using System.Threading.Tasks;

namespace FFF.IO
{
    internal static class TaskExtensions
    {
        internal static async Task<TResult> DefaultAfter<TResult>(this Task<TResult> task, TimeSpan timeout) =>
            await Task.WhenAny((Task)task, Task.Delay(timeout)).ConfigureAwait(false) != task ? default(TResult) : task.Result;

        internal static async Task CancelAfter(this Task task, TimeSpan timeout)
        {
            await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
        }
    }
}
