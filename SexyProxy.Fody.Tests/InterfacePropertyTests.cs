using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Fody.Tests
{
    [TestFixture]
    public class InterfacePropertyTests
    {
        [Test]
        public void InterfaceProperty()
        {
            PropertyInfo property = null;
            IPropertyClass proxy = new InterfacePropertyClass(new InvocationHandler(invocation =>
            {
                property = invocation.Property;
                return Task.FromResult<object>(null);
            }));
            proxy.Property = "foo";
            Assert.AreEqual(nameof(IPropertyClass.Property), property.Name.Split('.').Last());
        }

        public class InterfacePropertyClass : IProxy, IPropertyClass
        {
            string IPropertyClass.Property { get; set; }

            public InterfacePropertyClass(InvocationHandler invocationHandler)
            {
                InvocationHandler = invocationHandler;
            }

            public InvocationHandler InvocationHandler { get; }
        }

        public interface IPropertyClass
        {
            string Property { get; set; }
        }
    }
}