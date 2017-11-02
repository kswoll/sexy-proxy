using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class InvocationHandlerTests
    {
        [Test]
        public async Task NullObjectReturnedForBoolThrows()
        {
            var obj = Proxy.CreateProxy<IBoolMethod>(invocation => Task.FromResult<object>(null));
            try
            {
                await obj.BoolMethod();
                Assert.Fail("Expected InvalidAsyncException to have been thrown");
            }
            catch (InvalidAsyncException)
            {
                Assert.Pass();
            }
        }

        public interface IBoolMethod
        {
            Task<bool> BoolMethod();
        }
    }
}