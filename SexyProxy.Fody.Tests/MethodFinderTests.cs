using System.Collections.Generic;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class MethodFinderTests
    {
        [Test]
        public void FindsListStringMethod()
        {
            var testClass = Proxy.CreateProxy<TestClass>(async invocation => null);
        }

        public class TestClass : IProxy, ISetInvocationHandler
        {
            public InvocationHandler InvocationHandler { get; set; }

            public void ListStringMethod(List<string> s)
            {
            }
        }
    }
}