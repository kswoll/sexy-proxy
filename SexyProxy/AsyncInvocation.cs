using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class AsyncInvocationT<T> : Invocation
    {
        private Func<Invocation, Task<T>> implementation;

        public AsyncInvocationT(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments, Func<Invocation, Task<T>> implementation) : base(proxy, invocationHandler, method, arguments)
        {
            this.implementation = implementation;
        }

        public override async Task<object> Proceed()
        {
            return await implementation(this);
        }
    }
}
