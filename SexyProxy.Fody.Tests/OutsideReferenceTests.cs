using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class OutsideReferenceTests
    {
        [Test]
        public void GetHandler()
        {
            var proxy = new OutsideReferenceClass(x => Task.FromResult<object>("foo"));
            var result = proxy.GetHandler();
            Assert.IsNotNull(result);
        }

        [Test]
        public void SetHandler()
        {
            var proxy = new OutsideReferenceClass(x => Task.FromResult<object>("foo"));
            proxy.SetHandler(new InvocationHandler(x => Task.FromResult<object>("foo")));
        }

        public class OutsideReferenceClass : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            public OutsideReferenceClass(Func<Invocation, Task<object>> handler)
            {
                InvocationHandler = new InvocationHandler(handler);
            }

            public InvocationHandler GetHandler()
            {
                return new InvocationHandler(null);
            }

            public void SetHandler(InvocationHandler handler)
            {
            }
        }
    }
}