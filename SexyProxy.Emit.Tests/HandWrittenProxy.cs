using System.Threading.Tasks;

namespace SexyProxy.Emit.Tests
{
    public class HandWrittenProxy : IHandWritten
    {
        private IHandWritten target;
        private InvocationHandler invocationHandler;

        public HandWrittenProxy(IHandWritten target, InvocationHandler invocationHandler)
        {
            this.target = target;
            this.invocationHandler = invocationHandler;
        }

        public Task<string> GetStringAsync()
        {
            var method = typeof(IHandWritten).GetMethod("GetStringAsync");
            var arguments = new object[0];
            var invocation = new AsyncInvocationT<string>(method, arguments, args => target.GetStringAsync());
            return invocationHandler.AsyncInvokeT(invocation);
        }

        public Task DoSomethingAsync()
        {
            var method = typeof(IHandWritten).GetMethod("DoSomethingAsync");
            var arguments = new object[0];
            var invocation = new VoidAsyncInvocation(method, arguments, args => target.DoSomethingAsync());
            return invocationHandler.VoidAsyncInvoke(invocation);
        }

        public void DoSomething()
        {
            var method = typeof(IHandWritten).GetMethod("DoSomething");
            var arguments = new object[0];
            var invocation = new VoidInvocation(method, arguments, args => target.DoSomething());
            invocationHandler.VoidInvoke(invocation);
        }

        public string GetString()
        {
            var method = typeof(IHandWritten).GetMethod("GetString");
            var arguments = new object[0];
            var invocation = new InvocationT<string>(method, arguments, args => target.GetString());
            return invocationHandler.InvokeT(invocation);
        }

        public int Sum(int first, int second)
        {
            var method = typeof(IHandWritten).GetMethod("Sum");
            var arguments = new object[] { first, second };
            var invocation = new InvocationT<int>(method, arguments, args => target.Sum((int)args[0], (int)args[1]));
            return invocationHandler.InvokeT(invocation);
        }

        public Task<int> SumAsync(int first, int second)
        {
            var method = typeof(IHandWritten).GetMethod("SumAsync");
            var arguments = new object[] { first, second };
            var invocation = new AsyncInvocationT<int>(method, arguments, args => target.SumAsync((int)args[0], (int)args[1]));
            return invocationHandler.AsyncInvokeT(invocation);
        }

        public string StringProperty
        {
            get
            {
                var method = typeof(IHandWritten).GetProperty("StringProperty").GetMethod;
                var arguments = new object[0];
                var invocation = new InvocationT<string>(method, arguments, args => target.StringProperty);
                return invocationHandler.InvokeT(invocation);
            }
            set
            {
                var method = typeof(IHandWritten).GetProperty("StringProperty").GetMethod;
                var arguments = new object[] { value };
                var invocation = new VoidInvocation(method, arguments, args => target.StringProperty = (string)args[0]);
                invocationHandler.VoidInvoke(invocation);                
            }
        }
    }
}