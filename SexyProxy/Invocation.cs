using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class Invocation : InvocationBase
    {
        public abstract object Proceed();

        protected Invocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments)
            : base(proxy, invocationHandler, method, property, arguments)
        {
        }
    }

    public class InvocationT<T> : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.None;

        private Func<Invocation, T> implementation;

        public InvocationT(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments, Func<Invocation, T> implementation) : base(proxy, invocationHandler, method, property, arguments)
        {
            this.implementation = implementation;
        }

        public override object Proceed()
        {
            return implementation(this);
        }
    }
}
