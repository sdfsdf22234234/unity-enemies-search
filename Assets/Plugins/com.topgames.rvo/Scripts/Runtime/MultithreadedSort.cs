using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Nebukam.ORCA
{

    public static class MultithreadedSort
    {

        public static JobHandle Sort<[IsUnmanaged] T>(NativeArray<T> array, JobHandle parentHandle) where T : struct,  IComparable<T>
        {
            return MultithreadedSort.MergeSort<T>(array, new MultithreadedSort.SortRange(0, array.Length - 1), parentHandle);
        }


        private static JobHandle MergeSort<[IsUnmanaged] T>(NativeArray<T> array, MultithreadedSort.SortRange range, JobHandle parentHandle) where T : struct,  IComparable<T>
        {
            if (range.Length <= 400)
            {
                return new MultithreadedSort.QuicksortJob<T>
                {
                    array = array,
                    left = range.left,
                    right = range.right
                }.Schedule(parentHandle);
            }
            int middle = range.Middle;
            MultithreadedSort.SortRange sortRange = new MultithreadedSort.SortRange(range.left, middle);
            JobHandle job = MultithreadedSort.MergeSort<T>(array, sortRange, parentHandle);
            MultithreadedSort.SortRange sortRange2 = new MultithreadedSort.SortRange(middle + 1, range.right);
            JobHandle job2 = MultithreadedSort.MergeSort<T>(array, sortRange2, parentHandle);
            JobHandle dependsOn = JobHandle.CombineDependencies(job, job2);
            return new MultithreadedSort.Merge<T>
            {
                array = array,
                first = sortRange,
                second = sortRange2
            }.Schedule(dependsOn);
        }


        public const int QUICKSORT_THRESHOLD_LENGTH = 400;


        public readonly struct SortRange
        {

            public SortRange(int left, int right)
            {
                this.left = left;
                this.right = right;
            }



            public int Length
            {
                get
                {
                    return this.right - this.left + 1;
                }
            }



            public int Middle
            {
                get
                {
                    return this.left + this.right >> 1;
                }
            }



            public int Max
            {
                get
                {
                    return this.right;
                }
            }


            public readonly int left;


            public readonly int right;
        }


        [BurstCompile(CompileSynchronously = true)]
        public struct Merge<[IsUnmanaged] T> : IJob where T : struct,  IComparable<T>
        {

            public void Execute()
            {
                int num = this.first.left;
                int num2 = this.second.left;
                int num3 = this.first.left;
                NativeArray<T> nativeArray = new NativeArray<T>(this.second.right - this.first.left + 1, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = this.first.left; i <= this.second.right; i++)
                {
                    int index = i - this.first.left;
                    nativeArray[index] = this.array[i];
                }
                while (num <= this.first.Max || num2 <= this.second.Max)
                {
                    if (num <= this.first.Max && num2 <= this.second.Max)
                    {
                        T value = nativeArray[num - this.first.left];
                        T t = nativeArray[num2 - this.first.left];
                        if (value.CompareTo(t) < 0)
                        {
                            this.array[num3] = value;
                            num++;
                            num3++;
                        }
                        else
                        {
                            this.array[num3] = t;
                            num2++;
                            num3++;
                        }
                    }
                    else if (num <= this.first.Max)
                    {
                        T value2 = nativeArray[num - this.first.left];
                        this.array[num3] = value2;
                        num++;
                        num3++;
                    }
                    else if (num2 <= this.second.Max)
                    {
                        T value3 = nativeArray[num2 - this.first.left];
                        this.array[num3] = value3;
                        num2++;
                        num3++;
                    }
                }
                nativeArray.Dispose();
            }


            [NativeDisableContainerSafetyRestriction]
            public NativeArray<T> array;


            public MultithreadedSort.SortRange first;


            public MultithreadedSort.SortRange second;
        }


        [BurstCompile(CompileSynchronously = true)]
        public struct QuicksortJob<[IsUnmanaged] T> : IJob where T : struct,  IComparable<T>
        {

            public void Execute()
            {
                this.Quicksort(this.left, this.right);
            }


            private void Quicksort(int left, int right)
            {
                int i = left;
                int num = right;
                T other = this.array[(left + right) / 2];
                while (i <= num)
                {
                    for (; ; )
                    {
                        T t = this.array[i];
                        if (t.CompareTo(other) >= 0)
                        {
                            break;
                        }
                        i++;
                    }
                    for (; ; )
                    {
                        T t = this.array[num];
                        if (t.CompareTo(other) <= 0)
                        {
                            break;
                        }
                        num--;
                    }
                    if (i <= num)
                    {
                        T value = this.array[i];
                        this.array[i] = this.array[num];
                        this.array[num] = value;
                        i++;
                        num--;
                    }
                }
                if (left < num)
                {
                    this.Quicksort(left, num);
                }
                if (i < right)
                {
                    this.Quicksort(i, right);
                }
            }


            [NativeDisableContainerSafetyRestriction]
            public NativeArray<T> array;


            public int left;


            public int right;
        }
    }
}
