using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class InPlaceProxyTests
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

        [Test]
        public void AbstractNoChangeReturnsFoo()
        {
            var proxy = Proxy.CreateProxy<AbstractClass>(x => Task.FromResult<object>("foo"));
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);
        }

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

        public abstract class AbstractClass : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            protected AbstractClass(InvocationHandler handler)
            {
                InvocationHandler = handler;
            }

            public abstract string NoChange(int number);
        }

        [Test]
        public void InvocationHandlerSetter()
        {
            var proxy = Proxy.CreateProxy<SetInvocationHandler>(x => Task.FromResult<object>("foo"));
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);            
        }

        public abstract class SetInvocationHandler : IProxy, ISetInvocationHandler
        {
            public InvocationHandler InvocationHandler { get; set; }

            public abstract string NoChange(int number);
        }

        [Test]
        public async void AsyncClassWithInvocation()
        {
            var proxy = Proxy.CreateProxy<AsyncClass>(x => Task.FromResult<object>("foo"));
            var result = await proxy.AsyncMethod(0);
            Assert.AreEqual("foofoo", result);            
        }

        public class AsyncClass : IProxy, ISetInvocationHandler
        {
            public InvocationHandler InvocationHandler { get; set; }

            public async Task<string> AsyncMethod(int number)
            {
                var result = (string)await this.Invocation().Proceed();
                return result + result;
            }
        }

        [Test]
        public void PrivateMethod()
        {
            var proxy = Proxy.CreateProxy<ClassWithPrivateMethod>(x => Task.FromResult<object>("foo"));
            var result = proxy.PublicMethod("foo");
            Assert.AreEqual("foofoo", result);                        
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class ClassWithPrivateMethod : IProxy, ISetInvocationHandler
        {
            public InvocationHandler InvocationHandler { get; set; }

            private string PrivateMethod(string s)
            {
                var result = (string)this.Invocation().Proceed().Result;
                return result + result;
            }

            public string PublicMethod(string s)
            {
                return PrivateMethod(s);
            }
        }
    }
}