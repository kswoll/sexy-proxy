using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidAsyncInvocation : AsyncInvocation
    {
        public override InvocationFlags Flags => InvocationFlags.Void | InvocationFlags.Async;

        private Func<AsyncInvocation, Task> implementation;

        public VoidAsyncInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments, Func<AsyncInvocation, Task> implementation) : base(proxy, invocationHandler, method, property, arguments)
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
