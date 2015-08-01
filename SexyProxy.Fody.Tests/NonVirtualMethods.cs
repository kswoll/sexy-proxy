using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class NonVirtualMethods
    {
        [Test]
        public void MyMethodReturnsFoo()
        {
            var proxy = Proxy.CreateProxy<TestClass>(x => "foo");
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);
        }

        [Proxy]
        public class TestClass : IProxy
        {
            public InvocationHandler InvocationHandler { get; } = new InvocationHandler(Handler);

            private static Task<object> Handler(Invocation invocation)
            {
                return invocation.Proceed();
            }

            public string NoChange(int number)
            {
                var result = (string)this.Invocation().Proceed().Result;
                return result;
            }

            public string Alter(int number)
            {
                var result = this.Invocation().Proceed().Result;
                return (string)result + (string)result;
            }
        } 
    }
}