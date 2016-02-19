using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidInvocation : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.Void;

        private Action<Invocation> implementation;

        public VoidInvocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments, Action<Invocation> implementation) : base(proxy, invocationHandler, method, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            implementation(this);
            return Task.FromResult<object>(null);
        }
    }
}
