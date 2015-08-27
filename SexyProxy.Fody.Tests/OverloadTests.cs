using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class OverloadTests
    {
        [Test]
        public void Overloads()
        {
            var proxy = Proxy.CreateProxy<ClassWithOverloads>(async x => "foo" + await x.Proceed());
            var result = proxy.Method("foo");
            Assert.AreEqual("foofoofoo", result);                        
        }

        [Proxy]
        private class ClassWithOverloads
        {
            public virtual string Method()
            {
                return "foo";
            }

            public virtual string Method(string s)
            {
                return s + "foo";
            }

            public string Method<T, U>(T s) 
            {
                return s + "foo";
            }

            public string Method<T>(T s)
            {
                return s + "foo";
            }
        }              
    }
}