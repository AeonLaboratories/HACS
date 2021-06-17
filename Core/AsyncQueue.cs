using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HACS.Core
{
	public class AsyncQueue<T> : ConcurrentQueue<T>
	{
		private SemaphoreSlim semaphore;

		public AsyncQueue() : base() =>
			semaphore = new SemaphoreSlim(0);

		public new void Enqueue(T obj)
		{
			base.Enqueue(obj);
			semaphore.Release();
		}

		public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
		{
			while (true)
			{
				await semaphore.WaitAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (TryDequeue(out T item))
					return item;
			}
		}
	}
}
