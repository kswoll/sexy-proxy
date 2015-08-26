using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidInvocation : Invocation
    {
        private Action<object[]> implementation;

        public VoidInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments, Action<object[]> implementation) : base(proxy, invocationHandler, method, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            implementation(Arguments);
            return Task.FromResult<object>(null);
        }
    }
}
