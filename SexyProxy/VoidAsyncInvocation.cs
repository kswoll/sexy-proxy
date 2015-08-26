using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidAsyncInvocation : Invocation
    {
        private Func<Invocation, Task> implementation;

        public VoidAsyncInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments, Func<Invocation, Task> implementation) : base(proxy, invocationHandler, method, arguments)
        {
            this.implementation = implementation;
        }

        public override async Task<object> Proceed()
        {
            await implementation(this);
            return null;
        }
    }
}
