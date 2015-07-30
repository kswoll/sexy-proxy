using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class AsyncInvocationT<T> : Invocation
    {
        private Func<object[], Task<T>> implementation;

        public AsyncInvocationT(MethodInfo method, object[] arguments, Func<object[], Task<T>> implementation) : base(method, arguments)
        {
            this.implementation = implementation;
        }

        public override async Task<object> Proceed()
        {
            return await implementation(Arguments);
        }
    }
}
