using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleMultithreadQueue
{
	public partial class MultithreadQueue<T> {
		private TaskCompletionSource<byte> newElementWaitCompletionSource;

		//Asynchronously waits for a next element enqueue
		//Might to continue if the queue is already empty
		public async Task R_WaitAsync(int timeoutMs = -1) { 
			await newElementWaitCompletionSource.Task;
			newElementWaitCompletionSource = new TaskCompletionSource<byte>();
		}

		//Get next element or wait for a new one
		[ObsoleteAttribute("R_PopAllToNewQueue_WaitAsync is an experemental method")]
		public async Task<T> R_Dequeue_WaitAsync(int timeoutMs = -1) {
			T result;
			if(!R_Dequeue(out result)) { 
				await R_WaitAsync(timeoutMs);
				R_Dequeue(out result);
			}
			return result;
		}


		/*
		 * Returns all elements or waits for new
		 */
		[ObsoleteAttribute("R_PopAllToNewQueue_WaitAsync is an experemental method")]
		public async Task<Queue<T>> R_PopAllToNewQueue_WaitAsync(int timeoutMs = -1) {
			Queue<T> result;

			if(R_CheckMayHaveNew()) { 
				result = R_PopAllToNewQueue();
				if(result.Count != 0)
					return result;
			}

			await R_WaitAsync(timeoutMs);
			//Writer thread can enqueue new element here
			return R_PopAllToNewQueue();
			//And call newElementWaitCompletionSource.TrySetValue here
		}

	}
}
