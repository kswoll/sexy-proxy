using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class AsyncInvocation : InvocationBase
    {
        public abstract Task<object> Proceed();

        protected AsyncInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments)
            : base(proxy, invocationHandler, method, property, arguments)
        {
        }
    }

    public class AsyncInvocationT<T> : AsyncInvocation
    {
        public override InvocationFlags Flags => InvocationFlags.None;

        private Func<AsyncInvocation, T> implementation;

        public AsyncInvocationT(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments, Func<AsyncInvocation, T> implementation) : base(proxy, invocationHandler, method, property, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            return Task.FromResult<object>(implementation(this));
        }
    }
}