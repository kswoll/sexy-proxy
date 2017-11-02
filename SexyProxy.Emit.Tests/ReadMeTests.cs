using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class ReadMeTests
    {
        [Test]
        public async void NoProxy()
        {
            var writer = new StringWriter();
            await new HelloWorldPrinter().SayHello(writer);
            Assert.AreEqual("Hello World!", writer.ToString());
        }

        [Test]
        public async void WithProxy()
        {
            var writer = new StringWriter();
            var printer = Proxy.CreateProxy<HelloWorldPrinter>(async invocation =>
            {
                await writer.WriteAsync("John says, \"");
                await invocation.Proceed();
                await writer.WriteAsync("\"");
                return null;
            });
            await printer.SayHello(writer);
            Assert.AreEqual("John says, \"Hello World!\"", writer.ToString());
        }

        public class HelloWorldPrinter
        {
            public virtual async Task SayHello(TextWriter writer)
            {
                await writer.WriteAsync("Hello World!");
            }
        }
    }
}