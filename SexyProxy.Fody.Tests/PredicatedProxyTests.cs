using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class PredicatedProxyTests
    {
        [Test]
        public void DisabledProxy()
        {
            var proxy = new TestProxy(new InvocationHandler(
                invocation => Task.FromResult((object)"foo"),
                (_, method, property) => false));

            var value = proxy.StringProperty;
            Assert.IsNull(value);
        }

        [Test]
        public void EnableProxy()
        {
            var proxy = new TestProxy(new InvocationHandler(
                invocation => Task.FromResult((object)"foo"),
                (_, method, property) => true));

            var value = proxy.StringProperty;
            Assert.AreEqual("foo", value);
        }

        public class TestProxy : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            public TestProxy(InvocationHandler invocationHandler)
            {
                InvocationHandler = invocationHandler;
            }

            public string StringProperty { get; set; }
        }
    }
}