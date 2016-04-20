using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidAsyncInvocation : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.Void | InvocationFlags.Async;

        private Func<Invocation, Task> implementation;

        public VoidAsyncInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments, Func<Invocation, Task> implementation) : base(proxy, invocationHandler, method, property, arguments)
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
