public delegate uint GetWeight(object x);

// Fixed-size priority queue
public class PriorityQueue
{
    GetWeight weightOf;
    object[] store;
    int bottom;

    public bool IsEmpty { get { return bottom == 0; } }

    public PriorityQueue(int size, GetWeight getWeight)
    {
        weightOf = getWeight;
        store = new object[size];
        bottom = 0;
    }

    public void Push(object x)
    {
        var pos = bottom;
        store[pos] = x;
        while (pos > 0 && isLessThan(store[pos], store[parent(pos)]))
            pos = swap(pos, parent(pos));
        bottom++;
    }

    public object Pop()
    {
        var rv = store[0];
        store[0] = store[--bottom];
        store[bottom] = null;
        if (!IsEmpty)
        {
            for (int pos = 0, min = 0; ; )
            {
                if (lchild(pos) < store.Length && isGreaterThan(store[pos], store[lchild(pos)]))
                    min = lchild(pos);
                if (rchild(pos) < store.Length && isGreaterThan(store[min], store[rchild(pos)]))
                    min = rchild(pos);
                if (min == pos)
                    break;
                pos = swap(pos, min);
            }
        }
        return rv;
    }

    bool isLessThan(object x, object y)
    {
        if (x == null) return false;
        if (y == null) return true;
        return weightOf(x) < weightOf(y);
    }

    bool isGreaterThan(object x, object y)
    {
        if (x == null) return true;
        if (y == null) return false;
        return weightOf(x) > weightOf(y);
    }

    int parent(int pos)
    {
        return pos == 0 ? 0 : (pos - 1) / 2;
    }

    int lchild(int pos)
    {
        return 2 * pos + 1;
    }

    int rchild(int pos)
    {
        return 2 * pos + 2;
    }

    int swap(int x, int y)
    {
        var t = store[x];
        store[x] = store[y];
        store[y] = t;
        return y;
    }
}
