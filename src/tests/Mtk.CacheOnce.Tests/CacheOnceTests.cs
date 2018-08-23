using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Mtk.CacheOnce.Tests
{
    public class CacheOnceTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreate_MultipleThreads_InitOnceAndAllValuesTheSame(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    int val = cache.GetOrCreate(cnt, () => service.Init(), TimeSpan.FromDays(1));
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);

            var results = bag.ToArray();
            Assert.True(results.All(x => x == results[0]));
            Assert.Equal(1, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreate_MultipleThreadsWithTtlGetter_InitOnceAndAllValuesTheSame(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var val = cache.GetOrCreate(cnt, () => service.InitWithTtl(), v => v.Ttl).Value;
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);

            var results = bag.ToArray();
            Assert.True(results.All(x => x == results[0]));
            Assert.Equal(1, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreads_InitOnceAndAllValuesTheSame(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = await cache.GetOrCreateAsync(cnt, () => service.InitAsync(), TimeSpan.FromDays(1));
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);

            var results = bag.ToArray();
            Assert.True(results.All(x => x == results[0]));
            Assert.Equal(1, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreadsWithTtlGetter_InitOnceAndAllValuesTheSame(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = (await cache.GetOrCreateAsync(cnt, () => service.InitWithTtlAsync(), v => v.Ttl)).Value;
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);

            var results = bag.ToArray();
            Assert.True(results.All(x => x == results[0]));
            Assert.Equal(1, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreadsCallFailedMethod_FailedKeyRemovedThenInitAgain(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = await cache.GetOrCreateAsync(cnt, () => service.FailedAsync(), TimeSpan.FromDays(1));
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);
            await Task.Run(async () =>
            {
                int val = await cache.GetOrCreateAsync(cnt, () => service.FailedAsync(), TimeSpan.FromDays(1));
                bag.Add(val);
            });

            var results = bag.ToArray();
            Assert.True(results.All(x => x == default(int)));
            Assert.Equal(2, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreadsCallFailedMethodWithTtlGetter_FailedKeyRemovedThenInitAgain(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = (await cache.GetOrCreateAsync(cnt, () => service.FailedWithTtAsync(), v => v.Ttl)).Value;
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);
            await Task.Run(async () =>
            {
                int val = (await cache.GetOrCreateAsync(cnt, () => service.FailedWithTtAsync(), v => v.Ttl)).Value;
                bag.Add(val);
            });

            var results = bag.ToArray();
            Assert.True(results.All(x => x == default(int)));
            Assert.Equal(2, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreadsCallUnstableMethodWithRetryAsync_ValueFinallyInitiated(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();
            int successAfter = 4;

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = await cache.GetOrCreateAsync(cnt, () => service.UnstablepWithRetryAsync(successAfter), TimeSpan.FromDays(1));
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);
            await Task.Run(async () =>
            {
                int val = await cache.GetOrCreateAsync(cnt, () => service.UnstablepWithRetryAsync(successAfter), TimeSpan.FromDays(1));
                bag.Add(val);
            });

            var results = bag.ToArray();
            Assert.True(results.All(x => x == 200));
            Assert.Equal(successAfter, service.CountOfInitializations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetOrCreateAsync_MultipleThreadsCallUnstableMethodWithRetryAsyncWithTtlGetter_ValueFinallyInitiated(bool perKey)
        {
            var cnt = 20;
            var cache = new LocalCacheOnce(new MemoryCache(new MemoryCacheOptions()), perKey);
            var service = new TestInitializationService();
            var bag = new ConcurrentBag<int>();
            int successAfter = 4;

            var tasks = new Task[cnt];
            for (int i = 0; i < cnt; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    int val = (await cache.GetOrCreateAsync(cnt, () => service.UnstablepWithRetryWithTtlAsync(successAfter), v => v.Ttl)).Value;
                    bag.Add(val);
                });
            }
            await Task.WhenAll(tasks);
            await Task.Run(async () =>
            {
                int val = (await cache.GetOrCreateAsync(cnt, () => service.UnstablepWithRetryWithTtlAsync(successAfter), v => v.Ttl)).Value;
                bag.Add(val);
            });

            var results = bag.ToArray();
            Assert.True(results.All(x => x == 200));
            Assert.Equal(successAfter, service.CountOfInitializations);
        }
    }
}
