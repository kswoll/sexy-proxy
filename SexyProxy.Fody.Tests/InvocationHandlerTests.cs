using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class InvocationHandlerTests
    {
        [Test]
        public void AsyncModeThrow()
        {
            var invocationHandler = new InvocationHandler(
                async invocation =>
                {
                    await Task.Delay(1);
                    return 5;
                },
                asyncMode: AsyncInvocationMode.Throw);
            var proxy = Proxy.CreateProxy<IAsyncModeInterface>(invocationHandler);
            Assert.Throws<InvalidAsyncException>(() => proxy.Call());
        }

        [Test]
        public void AsyncModeWait()
        {
            var invocationHandler = new InvocationHandler(
                async invocation =>
                {
                    await Task.Delay(1);
                    return 5;
                },
                asyncMode: AsyncInvocationMode.Wait);
            var proxy = Proxy.CreateProxy<IAsyncModeInterface>(invocationHandler);
            var value = proxy.Call();
            Assert.AreEqual(5, value);
        }

        public interface IAsyncModeInterface
        {
            int Call();
        }
    }
}