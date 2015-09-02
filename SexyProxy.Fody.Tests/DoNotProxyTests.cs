using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class DoNotProxyTests
    {
        [Test]
        public void DoNotProxy()
        {
            string s = null;
            var obj = Proxy.CreateProxy<DoNotProxyClass>(invocation => s = "foo");
            obj.DoProxy = "bar";
            Assert.AreEqual("foo", s);

            s = null;
            obj.DoNotProxy = "baz";
            Assert.IsNull(s);
        }

        private class DoNotProxyClass : IProxy, ISetInvocationHandler
        {
            [DoNotProxy]public string DoNotProxy { get; set; }
            public string DoProxy { get; set; }


            public InvocationHandler InvocationHandler { get; set;  }
        }
    }
}