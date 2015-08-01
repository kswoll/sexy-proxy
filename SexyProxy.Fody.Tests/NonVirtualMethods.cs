using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class NonVirtualMethods
    {
        [Test]
        public void NoChangeReturnsFoo()
        {
            var proxy = new TestClass(x => Task.FromResult<object>("foo"));
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);
        }

        [Test]
        public void AlterReturns22()
        {
            var proxy = new TestClass(x => Task.FromResult<object>(x.Arguments[0].ToString()));
            var result = proxy.Alter(2);
            Assert.AreEqual("22", result);            
        }

        [Proxy]
        public class TestClass : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            public TestClass(Func<Invocation, Task<object>> handler)
            {
                InvocationHandler = new InvocationHandler(handler);
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