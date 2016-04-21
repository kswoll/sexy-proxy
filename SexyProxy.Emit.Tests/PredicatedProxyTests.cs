using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class PredicatedProxyTests
    {
        [Test]
        public void DisabledProxy()
        {
            var proxy = Proxy.CreateProxy<TestProxy>(x => "foo", (target, method, property) => false);
            var value = proxy.StringProperty;
            Assert.IsNull(value);
        }

        [Test]
        public void EnableProxy()
        {
            var proxy = Proxy.CreateProxy<TestProxy>(x => "foo", (target, method, property) => true);
            var value = proxy.StringProperty;
            Assert.AreEqual("foo", value);
        }

        public class TestProxy 
        {
            public virtual string StringProperty { get; set; }
        }
    }
}