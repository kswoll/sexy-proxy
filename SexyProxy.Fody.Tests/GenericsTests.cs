using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class GenericsTests 
    {
        [Test]
        public async void Get()
        {
            var proxy = Proxy.CreateProxy<ICrudApi<User>>(async invocation =>
            {
                await invocation.Proceed();
                var id = (int)invocation.Arguments[0];
                return new User { Id = id, FirstName = "John", LastName = "Doe" };
            });
            var user = await proxy.Get(5);
            Assert.AreEqual(5, user.Id);
            Assert.AreEqual("John", user.FirstName);
            Assert.AreEqual("Doe", user.LastName);
        }

        [Test]
        public async void GetAll()
        {
            var proxy = Proxy.CreateProxy<ICrudApi<User>>(async invocation =>
            {
                await invocation.Proceed();
                return new[] { new User { Id = 1, FirstName = "John", LastName = "Doe" } };
            });
            var users = await proxy.GetAll();
            Assert.AreEqual(1, users[0].Id);
            Assert.AreEqual("John", users[0].FirstName);
            Assert.AreEqual("Doe", users[0].LastName);
        }

        private class User
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        [Proxy]
        private interface ICrudApi<T> 
        {
            Task<T> Get(int id);
            Task<T[]> GetAll();
        }

        public class Foo<T>
        {
            public void LoadFunction()
            {
                Test(Nested.Bar);
            }

            private void Test(Func<T> func) { }

            public class Nested
            {
                public static T Bar()
                {
                    return default(T);
                }
            }
        }
    }
}