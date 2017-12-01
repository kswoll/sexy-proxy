using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class HandWrittenBaseNoTargetDynamicProxyTests
    {
        [Test]
        public async void GetStringAsync()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                await Task.Delay(1);
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            });
            var result = await proxy.GetStringAsync();
            Assert.AreEqual(HandWritten.GetStringAsyncReturnValue + "test", result);
        }

        [Test]
        public void GetString()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            });
            var result = proxy.GetString();
            Assert.AreEqual(HandWritten.GetStringReturnValue + "test", result);
        }

        [Test]
        public void GetStringThrowsExcpetionIfAsync()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                await Task.Delay(1);
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            });
            try
            {
                proxy.GetString();
                Assert.Fail("Should have thrown an InvalidAsyncException");
            }
            catch (InvalidAsyncException)
            {
            }
        }

        [Test]
        public async void DoSomethingAsync()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                await Task.Delay(1);
                await invocation.Proceed();
                return null;
            });
            await proxy.DoSomethingAsync();
            Assert.IsTrue(proxy.DoSomethingAsyncCalled);
        }

        [Test]
        public void DoSomething()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                await invocation.Proceed();
                return null;
            });
            proxy.DoSomething();
            Assert.IsTrue(proxy.DoSomethingCalled);
        }

        [Test]
        public void Sum()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            });
            var result = proxy.Sum(1, 2);
            Assert.AreEqual(6, result);
        }

        [Test]
        public async void SumAsync()
        {
            var proxy = Proxy.CreateProxyAsync<HandWritten>(null, async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            });
            var result = await proxy.SumAsync(1, 2);
            Assert.AreEqual(6, result);
        }
    }
}