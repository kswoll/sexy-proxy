using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
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

        [Proxy]
        public abstract class BaseClass
        {
            public abstract Task<string> GetAbstractHelloWorld();
        }
    }
}