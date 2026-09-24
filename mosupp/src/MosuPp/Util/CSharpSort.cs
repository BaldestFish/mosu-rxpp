using System;
using System.Collections.Generic;

namespace MosuPp.Util
{
    /// <summary>
    /// Port of rosu-pp's <c>util::sort::csharp</c>: the (unstable) introspective sort used by
    /// .NET Framework's <c>List&lt;T&gt;.Sort</c>. The order of equal elements must match the one
    /// osu!lazer produces, so the modern .NET sort cannot be used here.
    /// </summary>
    internal static class CSharpSort
    {
        private const int IntroSortSizeThreshold = 16;

        public static void Sort<T>(List<T> keys, Comparison<T> cmp)
        {
            if (keys.Count >= 2)
                IntroSort(keys, 0, keys.Count - 1, 2 * Log2(keys.Count), cmp);
        }

        private static int Log2(int n)
        {
            int r = 0;
            while ((n >>= 1) != 0) r++;
            return r;
        }

        private static void IntroSort<T>(List<T> keys, int lo, int hi, int depthLimit, Comparison<T> cmp)
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;

                if (partitionSize <= IntroSortSizeThreshold)
                {
                    switch (partitionSize)
                    {
                        case 1:
                            break;
                        case 2:
                            SwapIfGreater(keys, cmp, lo, hi);
                            break;
                        case 3:
                            SwapIfGreater(keys, cmp, lo, hi - 1);
                            SwapIfGreater(keys, cmp, lo, hi);
                            SwapIfGreater(keys, cmp, hi - 1, hi);
                            break;
                        default:
                            InsertionSort(keys, lo, hi, cmp);
                            break;
                    }

                    break;
                }

                if (depthLimit == 0)
                {
                    HeapSort(keys, lo, hi, cmp);
                    break;
                }

                depthLimit--;
                int p = PickPivotAndPartition(keys, lo, hi, cmp);
                IntroSort(keys, p + 1, hi, depthLimit, cmp);
                hi = p - 1;
            }
        }

        private static int PickPivotAndPartition<T>(List<T> keys, int lo, int hi, Comparison<T> cmp)
        {
            int mid = lo + (hi - lo) / 2;
            SwapIfGreater(keys, cmp, lo, mid);
            SwapIfGreater(keys, cmp, lo, hi);
            SwapIfGreater(keys, cmp, mid, hi);
            Swap(keys, mid, hi - 1);
            int left = lo;
            int right = hi - 1;
            int pivotIdx = right;

            while (left < right)
            {
                while (cmp(keys[++left], keys[pivotIdx]) < 0) { }
                while (cmp(keys[pivotIdx], keys[--right]) < 0) { }

                if (left >= right)
                    break;

                Swap(keys, left, right);
            }

            Swap(keys, left, hi - 1);

            return left;
        }

        private static void InsertionSort<T>(List<T> keys, int lo, int hi, Comparison<T> cmp)
        {
            for (int i = lo; i < hi; i++)
            {
                int j = i;
                T t = keys[i + 1];

                while (j >= lo && cmp(t, keys[j]) < 0)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }

                keys[j + 1] = t;
            }
        }

        private static void HeapSort<T>(List<T> keys, int lo, int hi, Comparison<T> cmp)
        {
            int n = hi - lo + 1;

            for (int i = n / 2; i >= 1; i--)
                DownHeap(keys, i, n, lo, cmp);

            for (int i = n; i > 1; i--)
            {
                Swap(keys, lo, lo + i - 1);
                DownHeap(keys, 1, i - 1, lo, cmp);
            }
        }

        private static void DownHeap<T>(List<T> keys, int i, int n, int lo, Comparison<T> cmp)
        {
            while (i <= n / 2)
            {
                int child = 2 * i;

                if (child < n && cmp(keys[lo + child - 1], keys[lo + child]) < 0)
                    child++;

                if (cmp(keys[lo + i - 1], keys[lo + child - 1]) >= 0)
                    break;

                Swap(keys, lo + i - 1, lo + child - 1);
                i = child;
            }
        }

        private static void SwapIfGreater<T>(List<T> keys, Comparison<T> cmp, int a, int b)
        {
            if (a != b && cmp(keys[a], keys[b]) > 0)
                Swap(keys, a, b);
        }

        private static void Swap<T>(List<T> keys, int i, int j)
        {
            if (i != j)
                (keys[i], keys[j]) = (keys[j], keys[i]);
        }
    }
}
