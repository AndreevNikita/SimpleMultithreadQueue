using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleMultithreadQueue
{

	/*
	 * Добавляет элементы много потоков, принимает один
	 */
	public partial class MultithreadQueue<T> : IEnumerable<T> {

		private object activeQueueMutex = new object();
		private Queue<T> activeQueue; //Input queue
		private Queue<T> bufferQueue; //Output queue
		public Queue<T> R_ReadyQueue { get => bufferQueue; } //Готовой называется очередь, которая уже изменяться не будет
		private volatile bool mayHaveNew;
		private AutoResetEvent newElementSignal;

		[ObsoleteAttribute("An experemental property")]
		public WaitHandle NewElementWaiter { get => newElementSignal; }


		public MultithreadQueue() {
			activeQueue = new Queue<T>();
			bufferQueue = new Queue<T>();
			newElementSignal = new AutoResetEvent(false);
			mayHaveNew = false;
			newElementWaitCompletionSource = new TaskCompletionSource<byte>();
		}

		public void Enqueue(T obj) {
			/*
			 * Вот здесь может получить ссылку на activeQueue, далее сработал Interlocked.Exchange, далее Enqueue и мы кладём элемент в ушедшую очередь
			 * Сделать lock?
			 */
			lock(activeQueueMutex) {
				activeQueue.Enqueue(obj);
			}
			Interlocked.MemoryBarrier();
			mayHaveNew = true;
			newElementSignal.Set();
			newElementWaitCompletionSource.TrySetResult(0);
		}

		/*		Методы ниже вызываются только читающим потоком		*/

		//Вспомогательный метод, меняющий местами входную очередь и опустошённую выходную
		public void R_Swap() {
			lock(activeQueueMutex) {
				Swap(ref activeQueue, ref bufferQueue);
			}
		}

		//Удаляет элемент из непополняемой очереди
		public bool R_DequeueReady(out T nextObj, bool swapAutomatically = true) {
			if(bufferQueue.Count != 0) {
				nextObj = bufferQueue.Dequeue();
				return true;
			}

			if(swapAutomatically)
				R_Swap();

			nextObj = default;
			return false;
		}

		//Возвращает элемент из очереди, удаляя его
		public bool R_Dequeue(out T nextObj) {
			if(bufferQueue.Count != 0) {
				nextObj = bufferQueue.Dequeue();
				return true;
			}

			//Если буфер пуст, меняем местами с текущей очередью
			R_Swap();
			
			if(bufferQueue.Count != 0) {
				nextObj = bufferQueue.Dequeue();
				return true;
			}

			//Если текущая очередь (теперь буфер) тоже пуста, возвращаем false
			nextObj = default;
			return false;
		}

		//Waits for a next element enqueue
		//Might to continue if the queue is already empty
		public void Wait(int timeoutMs = -1) { 
			newElementSignal.WaitOne(timeoutMs);
		}

		public void SkipWait() { 
			newElementSignal.Set();
			newElementWaitCompletionSource.TrySetResult(0);
		}

		//Returns true if queue can contain new element, never returns false, if queue isn't empty
		[ObsoleteAttribute("R_CheckMayHaveNew is an experemental method")]
		public bool R_CheckMayHaveNew() { 
			return mayHaveNew;
		}

		private void R_ResetMayHaveNewFlag() { 
			mayHaveNew = false;
		}

		//Get next element or wait for a new one
		[ObsoleteAttribute("R_Dequeue_Wait is an experemental method")]
		public T R_Dequeue_Wait(int timeoutMs = -1) {
			T result;
			if(!R_Dequeue(out result)) { 
				Wait(timeoutMs);
				R_Dequeue(out result);
			}
			return result;
		}

		//Вернуть все элементы, в объекте новой очереди, исключив из текущей
		//(гораздо проще создать новую очередь и поменять местами входные и выходные (в текущей оказываются пустые очереди, в возвращаемой всё что было в текущей))
		public MultithreadQueue<T> R_PopToNewMultithreadQueue() {
			R_ResetMayHaveNewFlag();
			MultithreadQueue<T> result = new MultithreadQueue<T>();
			Swap(ref bufferQueue, ref result.bufferQueue);
			bufferQueue = result.activeQueue;
			R_Swap();
			result.activeQueue = bufferQueue;
			return result;
		}

		/*
		 * Swap queues and returns buffer
		 */
		public Queue<T> R_PopAllToNewQueue() {
			R_ResetMayHaveNewFlag();
			//Save buffer queue if is not empty
			if(bufferQueue.Count != 0) { 
				MultithreadQueue<T> popQueue = R_PopToNewMultithreadQueue();
				foreach(T element in popQueue.activeQueue)
					popQueue.bufferQueue.Enqueue(element);
				return popQueue.bufferQueue;
			} else { 
				R_Swap();

				Queue<T> result = bufferQueue;
				bufferQueue = new Queue<T>();
				return result;
			}
			
		}

		/*
		 * Returns all elements or waits for new
		 */
		[ObsoleteAttribute("R_PopAllToNewQueue_Wait is an experemental method")]
		public Queue<T> R_PopAllToNewQueue_Wait(int timeoutMs = -1) {
			Queue<T> result;

			if(R_CheckMayHaveNew()) { 
				result = R_PopAllToNewQueue();
				if(result.Count != 0)
					return result;
			}

			Wait(timeoutMs);
			//Writer thread can enqueue new element here
			return R_PopAllToNewQueue();
			//And call newElementSignal.Set() here
		}

		public void R_Clear() { 
			activeQueue = new Queue<T>();
			bufferQueue = new Queue<T>();
		}

		public bool R_IsEmpty() { 
			if(bufferQueue.Count != 0)
				return false;
			if(activeQueue.Count != 0) //Can be outdated, but it's for tick systems
				return false;

			return true;
		}


		public IEnumerable<T> R_PopAll() { 
			while(bufferQueue.Count != 0) { 
				yield return bufferQueue.Dequeue();
			}
			R_Swap();
			while(bufferQueue.Count != 0) { 
				yield return bufferQueue.Dequeue();
			}
		}

		public IEnumerator<T> GetEnumerator() {
			return new QueueEnumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		#region QueueEnumerator

		public class QueueEnumerator : IEnumerator<T> {
			private IEnumerator<T> _firstEnum;
			private IEnumerator<T> _secondEnum;
			private IEnumerator<T> _currentEnum;

			public T Current { get => _currentEnum.Current; }

			object IEnumerator.Current { get => Current; }

			public QueueEnumerator(MultithreadQueue<T> source) { 
				_firstEnum = source.bufferQueue.GetEnumerator();
				_secondEnum = source.bufferQueue.GetEnumerator();
				_currentEnum = _firstEnum;
			}

			public void Dispose() {
				_firstEnum.Dispose();
				_secondEnum.Dispose();
			}

			public bool MoveNext() {
				if(_currentEnum.MoveNext()) { 
					return true;
				} else { 
					if(_currentEnum == _firstEnum) { 
						_currentEnum = _secondEnum;
						return MoveNext();
					} else { 
						return false;
					}
				} 
			}

			public void Reset() {
				_firstEnum.Reset();
				_currentEnum.Reset();
				_currentEnum = _firstEnum;
			}
		}

		#endregion

		public static void Swap<TSwapType>(ref TSwapType a, ref TSwapType b) {
			TSwapType buffer = a;
			a = b;
			b = buffer;
		}
	}


}
