﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class InPlaceProxyTests
    {
/*
        [Test]
        public void GenericMethod()
        {
            var proxy = new GenericMethods(new InvocationHandler(async invocation =>
            {
                return int.Parse(invocation.Arguments.Single().ToString());
            }));
            proxy.Foo(5);
        }

        private class GenericMethods : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            public GenericMethods(InvocationHandler invocationHandler)
            {
                InvocationHandler = invocationHandler;
            }

            public T Foo<T>(T value)
            {
                return default(T);
            }
        }

*/
        [Test]
        public void NoChangeReturnsFoo()
        {
            var proxy = new TestClass(x => Task.FromResult<object>("foo"));
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);
        }

        [Test]
        public void AlterReturns2()
        {
            var proxy = new TestClass(x => Task.FromResult<object>(x.Arguments[0].ToString()));
            var result = proxy.Alter(2);
            Assert.AreEqual("2", result);            
        }

        [Test]
        public void AbstractNoChangeReturnsFoo()
        {
            var proxy = Proxy.CreateProxy<AbstractClass>(x => Task.FromResult<object>("foo"));
            var result = proxy.NoChange(0);
            Assert.AreEqual("foo", result);
        }

        [Test]
        public void StringProperty()
        {
            var proxy = new TestClass(x => Task.FromResult<object>("foo"));
            var result = proxy.StringProperty;
            Assert.AreEqual("foo", result);
        }

        public class TestClass : IProxy
        {
            public InvocationHandler InvocationHandler { get; }

            public TestClass(Func<Invocation, Task<object>> handler)
            {
                InvocationHandler = new InvocationHandler(handler);
            }

            public string StringProperty { get; set; }

            public string NoChange(int number)
            {
                return "foo";
            }

            public string Alter(int number)
            {
                return number.ToString();
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
                await Task.Delay(1);
                return "foo";
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
                return s;
            }

            public string PublicMethod(string s)
            {
                return PrivateMethod(s);
            }
        }

        [Test]
        public void Overloads()
        {
            var proxy = Proxy.CreateProxy<ClassWithOverloads>(x => Task.FromResult<object>("foo"));
            var result = proxy.Method("foo");
            Assert.AreEqual("foofoo", result);                        
        }

        [Proxy]
        private class ClassWithOverloads
        {
            public string Method()
            {
                return "foo";
            }

            public string Method(string s)
            {
                return s + "foo";
            }

            public string Method<T, U>(T s) where T : Dictionary<string, T>
            {
                return s + "foo";
            }

            public string Method<T>(T s) where T : List<T>
            {
                return s + "foo";
            }

            public string Method<T>(object s)
            {
                return s + "foo";
            }
        }     
    }
}