# SimpleMultithreadQueue
Queue for multithread programs with one queue reader and several writers

## How it works
MultithreadQueue<T> contains two objects Queue<T> one is an active queue (current queue) and one is a buffer
While read thread doesn't pops elements from MultithreadQueue, write threads adds new elements to **activeQueue**. When read thread requires new element, algorithm checks, if **bufferQueue** has elements, pops from **bufferQueue**, swaps queues else and pops from new buffer (empty **bufferQueue** becames **activeQueue** to receive new elements), else returns default and false.

## Methods

### For writers
* `Enqueue({Element])` - add element to queue

### For readers
Only reader's thread methods marked by prefix "R_"
* `void R_Swap()` - swaps active and buffer queues
* `bool R_DequeueReady(out T nextObj, bool swapAutomatically = true)` - **nextObj** - next element in bufferQueue, **swapAutomatically** - flag to swap queues (returns element only from **bufferQueue** if the bufferQueue is empty, swaps queues and returns default and false)
* `bool R_Dequeue(out T nextObj)` - deq dequeues next element (default) and returns success status
* `MultithreadQueue<T> R_PopToNewQueue()` - returns new MultithreadQueue with all elements and clears current queues
* `Queue<T> R_PopAllToNewQueue()` - returns queue with all elements on the call time.
* `IEnumerable<T> R_PopAll()` pops all elements from queue with enumerable (can be used in foreach cycle) and dequeue their
* `void R_Clear()` clears *MultithreadQueue*
* `bool R_IsEmpty` check if the MultithreadQueue is empty

#### Experemental reader wait for new methods
* `Queue<T> R_PopAllToNewQueue_Wait()` - returns all elements on the call time or waits for new elements if empty
* `T R_Dequeue_Wait()` - get next element or wait for a new one
* `void R_Wait()` - wait for new element
* `bool R_CheckMayHaveNew()` - returns *true* if queue can contain new element, never returns *false*, if queue isn't empty

Also MultithreadQueue<T> implements IEnumerator<T> interface and can be used in foreach cycles (without elements dequeue)