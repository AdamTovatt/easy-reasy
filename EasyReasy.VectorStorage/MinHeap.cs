using System.Runtime.CompilerServices;

namespace EasyReasy.VectorStorage
{
    internal class MinHeap<T>
    {
        private readonly T[] _items;
        private readonly float[] _priorities;
        private int _count;
        private readonly int _maxSize;

        public MinHeap(int maxSize)
        {
            _maxSize = maxSize;
            _items = new T[maxSize];
            _priorities = new float[maxSize];
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item, float priority)
        {
            if (_count < _maxSize)
            {
                _items[_count] = item;
                _priorities[_count] = priority;
                BubbleUp(_count);
                _count++;
            }
            else if (priority > _priorities[0])
            {
                _items[0] = item;
                _priorities[0] = priority;
                BubbleDown(0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> GetItems()
        {
            return new ReadOnlySpan<T>(_items, 0, _count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_priorities[index] >= _priorities[parentIndex])
                    break;

                // Swap items
                T tempItem = _items[index];
                _items[index] = _items[parentIndex];
                _items[parentIndex] = tempItem;

                // Swap priorities
                float tempPriority = _priorities[index];
                _priorities[index] = _priorities[parentIndex];
                _priorities[parentIndex] = tempPriority;

                index = parentIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BubbleDown(int index)
        {
            while (true)
            {
                int leftChild = (2 * index) + 1;
                int rightChild = (2 * index) + 2;
                int smallest = index;

                if (leftChild < _count && _priorities[leftChild] < _priorities[smallest])
                    smallest = leftChild;

                if (rightChild < _count && _priorities[rightChild] < _priorities[smallest])
                    smallest = rightChild;

                if (smallest == index)
                    break;

                // Swap items
                T tempItem = _items[index];
                _items[index] = _items[smallest];
                _items[smallest] = tempItem;

                // Swap priorities
                float tempPriority = _priorities[index];
                _priorities[index] = _priorities[smallest];
                _priorities[smallest] = tempPriority;

                index = smallest;
            }
        }
    }
}