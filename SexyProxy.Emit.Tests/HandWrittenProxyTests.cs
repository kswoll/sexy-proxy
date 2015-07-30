using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class HandWrittenProxyTests
    {
        [Test]
        public async void GetStringAsync()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                await Task.Delay(1);
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            }));
            var result = await proxy.GetStringAsync();
            Assert.AreEqual(HandWritten.GetStringAsyncReturnValue + "test", result);
        }

        [Test]
        public void GetString()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            }));
            var result = proxy.GetString();
            Assert.AreEqual(HandWritten.GetStringReturnValue + "test", result);
        }

        [Test]
        public void StringPropertyGet()
        {
            var handWritten = new HandWritten();
            handWritten.StringProperty = "AStringProperty";
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            }));
            var result = proxy.StringProperty;
            Assert.AreEqual("AStringProperty" + "test", result);
        }

        [Test]
        public void StringPropertySet()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                invocation.Arguments[0] = "AStringValue" + invocation.Arguments[0];
                await invocation.Proceed();
                return null;
            }));
            proxy.StringProperty = "test";
            Assert.AreEqual("AStringValue" + "test", handWritten.StringProperty);
        }

        [Test]
        public void GetStringThrowsExcpetionIfAsync()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                await Task.Delay(1);
                var returnValue = await invocation.Proceed();
                return (string)returnValue + "test";
            }));
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
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                await Task.Delay(1);
                await invocation.Proceed();
                return null;
            }));
            await proxy.DoSomethingAsync();
            Assert.IsTrue(handWritten.DoSomethingAsyncCalled);
        }

        [Test]
        public void DoSomething()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                await invocation.Proceed();
                return null;
            }));
            proxy.DoSomething();
            Assert.IsTrue(handWritten.DoSomethingCalled);
        }

        [Test]
        public void Sum()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            }));
            var result = proxy.Sum(1, 2);
            Assert.AreEqual(6, result);
        }

        [Test]
        public async void SumAsync()
        {
            var handWritten = new HandWritten();
            var proxy = new HandWrittenProxy(handWritten, new InvocationHandler(async invocation =>
            {
                var value = (int)await invocation.Proceed();
                return value + 3;
            }));
            var result = await proxy.SumAsync(1, 2);
            Assert.AreEqual(6, result);
        }
    }
}