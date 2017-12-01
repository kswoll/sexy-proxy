using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class Invocation : InvocationBase, IAsyncInvocation
    {
        public abstract object Proceed();

        protected Invocation(object proxy, MethodInfo method, PropertyInfo property, object[] arguments)
            : base(proxy, method, property, arguments)
        {
        }

        Task<object> IAsyncInvocation.Proceed()
        {
            return Task.FromResult(Proceed());
        }
    }

    public class InvocationT<T> : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.None;

        private Func<Invocation, T> implementation;

        public InvocationT(object proxy, MethodInfo method, PropertyInfo property, object[] arguments, Func<Invocation, T> implementation) : base(proxy, method, property, arguments)
        {
            this.implementation = implementation;
        }

        public override object Proceed()
        {
            return implementation(this);
        }
    }
}
