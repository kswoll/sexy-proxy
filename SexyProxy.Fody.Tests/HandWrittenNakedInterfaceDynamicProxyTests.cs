using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class HandWrittenNakedInterfaceDynamicProxy
    {
        [Test]
        public async Task GetStringAsync()
        {
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                await Task.Delay(1);
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            });
            var result = await proxy.GetStringAsync();
            Assert.AreEqual("test", result);
        }

        [Test]
        public void GetString()
        {
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            });
            var result = proxy.GetString();
            Assert.AreEqual("test", result);
        }

        [Test]
        public void GetStringThrowsExcpetionIfAsync()
        {
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
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
        public async Task DoSomethingAsync()
        {
            var doSomethingAsyncCalled = false;
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                await Task.Delay(1);
                await invocation.Proceed();
                doSomethingAsyncCalled = true;
                return null;
            });
            await proxy.DoSomethingAsync();
            Assert.IsTrue(doSomethingAsyncCalled);
        }

        [Test]
        public void DoSomething()
        {
            var doSomethingCalled = false;
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                await invocation.Proceed();
                doSomethingCalled = true;
                return null;
            });
            proxy.DoSomething();
            Assert.IsTrue(doSomethingCalled);
        }

        [Test]
        public void Sum()
        {
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            });
            var result = proxy.Sum(1, 2);
            Assert.AreEqual(3, result);
        }

        [Test]
        public async Task SumAsync()
        {
            var proxy = Proxy.CreateProxy<IHandWritten>(null, async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            });
            var result = await proxy.SumAsync(1, 2);
            Assert.AreEqual(3, result);
        }
    }
}