namespace EasyReasy.VectorStorage
{
    internal class MinHeap<T>
    {
        private readonly List<(T Item, float Priority)> _heap = new();
        private readonly int _maxSize;

        public MinHeap(int maxSize)
        {
            _maxSize = maxSize;
        }

        public void Add(T item, float priority)
        {
            if (_heap.Count < _maxSize)
            {
                _heap.Add((item, priority));
                BubbleUp(_heap.Count - 1);
            }
            else if (priority > _heap[0].Priority)
            {
                _heap[0] = (item, priority);
                BubbleDown(0);
            }
        }

        public List<T> GetItems()
        {
            List<T> result = new List<T>(_heap.Count);
            for (int i = 0; i < _heap.Count; i++)
            {
                result.Add(_heap[i].Item);
            }
            return result;
        }

        private void BubbleUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_heap[index].Priority >= _heap[parentIndex].Priority)
                    break;

                (_heap[index], _heap[parentIndex]) = (_heap[parentIndex], _heap[index]);
                index = parentIndex;
            }
        }

        private void BubbleDown(int index)
        {
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int smallest = index;

                if (leftChild < _heap.Count && _heap[leftChild].Priority < _heap[smallest].Priority)
                    smallest = leftChild;

                if (rightChild < _heap.Count && _heap[rightChild].Priority < _heap[smallest].Priority)
                    smallest = rightChild;

                if (smallest == index)
                    break;

                (_heap[index], _heap[smallest]) = (_heap[smallest], _heap[index]);
                index = smallest;
            }
        }
    }
}