using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleMultithreadQueue {

	/*
	 * Добавляет элементы много потоков, принимает один
	 */
	public class MultithreadQueue<T> : IEnumerable<T> {

		private Queue<T> activeQueue; //Input queue
		private Queue<T> bufferQueue; //Output queue
		public Queue<T> R_ReadyQueue { get => bufferQueue; } //Готовой называется очередь, которая уже изменяться не будет

		public MultithreadQueue() {
			activeQueue = new Queue<T>();
			bufferQueue = new Queue<T>();
		}

		public void Enqueue(T obj) {
			lock(activeQueue) {
				activeQueue.Enqueue(obj);
			}
		}

		/*		Методы ниже вызываются только читающим потоком		*/

		//Вспомогательный метод, меняющий местами входную очередь и опустошённую выходную
		public void R_Swap() {
			lock(activeQueue) {
				swap(ref activeQueue, ref bufferQueue);
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



		//Вернуть все элементы, в объекте новой очереди, исключив из текущей
		//(гораздо проще создать новую очередь и поменять местами входные и выходные (в текущей оказываются пустые очереди, в возвращаемой всё что было в текущей))
		public MultithreadQueue<T> R_PopToNewMultithreadQueue() {
			MultithreadQueue<T> result = new MultithreadQueue<T>();
			swap(ref bufferQueue, ref result.bufferQueue);
			lock(activeQueue) {
				swap(ref activeQueue, ref result.activeQueue);
			}
			return result;
		}

		/*
		 * Swap queues and returns buffer
		 */
		public Queue<T> R_PopAllToNewQueue() {
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

		public void R_Clear() { 
			Queue<T> buffer = new Queue<T>();
			lock(activeQueue) {
				swap(ref activeQueue, ref buffer);
			}
			bufferQueue = new Queue<T>();
		}

		public bool R_IsEmpty() { 
			if(bufferQueue.Count != 0)
				return false;
			lock(activeQueue) { 
				if(activeQueue.Count != 0)
					return false;
			}
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

		public static void swap<T>(ref T a, ref T b) {
			T buffer = a;
			a = b;
			b = buffer;
		}
	}


}
