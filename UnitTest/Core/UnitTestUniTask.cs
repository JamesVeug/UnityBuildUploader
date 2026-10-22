using System.Threading.Tasks;

// Stands in for UniTask (com.cysharp.unitask), which InvokeStaticMethodAction supports without referencing it.
// The namespace, type names and AsTask extensions match UniTask 2.x, so the action finds them the same way. This
// assembly does not reference the real UniTask, and the action only looks for AsTask in the assembly that defines
// the returned type, so the two never mix in a project that has both.
namespace Cysharp.Threading.Tasks
{
    public readonly struct UniTask
    {
        internal readonly Task Task;

        public UniTask(Task task)
        {
            Task = task;
        }
    }

    public readonly struct UniTask<T>
    {
        internal readonly Task<T> Task;

        public UniTask(Task<T> task)
        {
            Task = task;
        }
    }

    public static class UniTaskExtensions
    {
        public static Task AsTask(this UniTask task) => task.Task;

        public static Task<T> AsTask<T>(this UniTask<T> task) => task.Task;
    }
}
