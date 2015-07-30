using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class AbstractMethodTests
    {
        [Test]
        public async void AbstractMethod()
        {
            var proxy = Proxy.CreateProxy<BaseClass>(async invocation =>
            {
                var baseValue = await invocation.Proceed();
                return baseValue + "Test";
            });
            var s = await proxy.GetAbstractHelloWorld();
            Assert.AreEqual("Test", s);
        }

        public abstract class BaseClass
        {
            public abstract Task<string> GetAbstractHelloWorld();
        }
    }
}