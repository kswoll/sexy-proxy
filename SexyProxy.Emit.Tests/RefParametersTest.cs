using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SexyProxy.Emit.Tests
{
    [TestFixture]
    public class RefParametersTest
    {
        private string refParam;
        private string outParam;
        private string resultString;
        private ITestClassWithRefParameters testClassWithRefParameters;
        private ITestClassWithRefParameters proxy;
        private MyTestRefClass myTestRefClass = new MyTestRefClass { Value = 10 };

        [SetUp]
        protected void SetUp()
        {
            refParam = "RefParam";
            testClassWithRefParameters = new TestClassWithRefParameters();
            proxy = Proxy.CreateProxy(testClassWithRefParameters, async invocation =>
            {
                var result = await invocation.Proceed();
                return result;
            });

        }

        [Test]
        public void DoSomething()
        {
            proxy.DoSomething(1, ref refParam, out outParam);
            ParametersAssert();
        }

        [Test]
        public void GetSomeString()
        {
            resultString = proxy.GetSomeString(1, ref refParam, out outParam);
            ParametersAssert();
            ResultStringAssert();
        }

        [Test]
        public void DoSomethingWithClassByRef()
        {
            proxy.DoSomethingWithClassByRef(ref myTestRefClass);
            Assert.AreEqual(myTestRefClass.Value, 0);
        }

        [Test]
        public void GetValueFromClassWithRef()
        {
            var result = proxy.GetValueFromClassWithRef(ref myTestRefClass);
            Assert.AreEqual(myTestRefClass.Value, 0);
            Assert.AreEqual(result, 0);
        }

        [Test]
        public async void DoSomethingAsync()
        {
            resultString = await proxy.DoSomethingAsync();
            ResultStringAssert();
        }


        private void ResultStringAssert()
        {
            Assert.AreEqual("ResultString", resultString);
        }

        private void ParametersAssert()
        {
            Assert.AreEqual("OutParam", outParam);
            Assert.AreEqual("RefParamModified", refParam);
        }

        public interface ITestClassWithRefParameters
        {
            void DoSomething(int simpleParam, ref string refParam, out string outParam);

            string GetSomeString(int simpleParam, ref string refParam, out string outParam);

            void DoSomethingWithClassByRef(ref MyTestRefClass refClass);

            int GetValueFromClassWithRef(ref MyTestRefClass refClass);

            Task<string> DoSomethingAsync();
        }

        private class TestClassWithRefParameters : ITestClassWithRefParameters
        {
            public void DoSomething(int simpleParam, ref string refParam, out string outParam)
            {
                ModifyRefParameters(ref refParam, out outParam);
            }

            public string GetSomeString(int simpleParam, ref string refParam, out string outParam)
            {
                ModifyRefParameters(ref refParam, out outParam);
                return "ResultString";
            }

            public void DoSomethingWithClassByRef(ref MyTestRefClass refClass)
            {
                refClass = new MyTestRefClass { Value = 0};
            }

            public int GetValueFromClassWithRef(ref MyTestRefClass refClass)
            {
                DoSomethingWithClassByRef(ref refClass);
                return refClass.Value;
            }

            public async Task<string> DoSomethingAsync()
            {
                await GetValueAsync();
                return "ResultString";
            }

            private Task GetValueAsync()
            {
                return Task.CompletedTask;
            }

            private void ModifyRefParameters(ref string refParam, out string outParam)
            {
                refParam+="Modified";
                outParam = "OutParam";
            }
        }

        public class MyTestRefClass
        {
            public int Value { get; set; }
        }
    }
}