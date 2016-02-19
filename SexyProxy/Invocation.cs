using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class Invocation
    {
        public object Proxy { get; }
        public InvocationHandler InvocationHandler { get; }
        public MethodInfo Method { get; }
        public object[] Arguments { get; }

        public abstract Task<object> Proceed();
        public abstract InvocationFlags Flags { get; }

        protected Invocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments)
        {
            Proxy = proxy;
            InvocationHandler = invocationHandler;
            Method = method;
            Arguments = arguments;
        }

        /// <summary>
        /// Convenience method so the parameter to HasFlag is typed to InvocationFlags rather than just Enum.
        /// </summary>
        public bool HasFlag(InvocationFlags flag)
        {
            return Flags.HasFlag(flag);
        }
    }

    public class InvocationT<T> : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.None;

        private Func<Invocation, T> implementation;

        public InvocationT(object proxy, InvocationHandler invocationHandler, MethodInfo method, object[] arguments, Func<Invocation, T> implementation) : base(proxy, invocationHandler, method, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            return Task.FromResult<object>(implementation(this));
        }
    }
}
