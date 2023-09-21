using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Extensions.ObjectPool;
using System.Security.Cryptography;

namespace SHA1InitializationBenchmark
{
    [MemoryDiagnoser]
    public class SHA1InitializationBenchmarks
    {
        const int HashCount = 5000;
        const int PartitionSize = 500;

        private static CountdownEvent countdown;

        private readonly byte[][] sourceData;
        private readonly byte[][] resultData;

        public SHA1InitializationBenchmarks()
        {
            Random random = new();

            this.sourceData = Enumerable.Range(0, HashCount).Select(_ =>
            {
                var result = new byte[200];
                random.NextBytes(result);
                return result;
            }).ToArray();

            resultData = new byte[this.sourceData.Length][];
        }

        [Benchmark]
        public void NewHashAlgorithmPerRow()
        {
            this.CalculateHashesInParallel(
                SHA1.Create,
                hasher => hasher.Dispose());
        }

        [Benchmark]
        public void UsingObjectPool()
        {
            var objectPoolProvider = new DefaultObjectPoolProvider();
            var objectPool = objectPoolProvider.Create(new SHA1PooledObjectPolicy());

            this.CalculateHashesInParallel(objectPool.Get, objectPool.Return);
        }

        private void CalculateHashesInParallel(Func<SHA1> getHasher, Action<SHA1> releaseHasher)
        {
            int partitionCount = HashCount / PartitionSize;

            countdown = new CountdownEvent(partitionCount);

            for (int partitionNumber = 0; partitionNumber < partitionCount; ++partitionNumber)
            {
                int start = partitionNumber * PartitionSize;
                Thread newThread = new(new ThreadStart(this.GetHashCalculatorForRange(start, start + PartitionSize, getHasher, releaseHasher)));
                newThread.Start();
            }

            countdown.Wait();
        }

        private Action GetHashCalculatorForRange(int fromInclusive, int toExclusive, Func<SHA1> getHasher, Action<SHA1> releaseHasher)
        {
            return () =>
            {
                for (int i = fromInclusive; i < toExclusive; ++i)
                {
                    SHA1 hasher = getHasher();
                    try
                    {
                        this.resultData[i] = hasher.ComputeHash(this.sourceData[i]);
                    }
                    finally
                    {
                        releaseHasher(hasher);
                    }
                }

                countdown.Signal();
            };
        }

    }

    public class SHA1PooledObjectPolicy : IPooledObjectPolicy<SHA1>
    {
        public SHA1 Create() => SHA1.Create();

        public bool Return(SHA1 obj) => true;
    }
}


