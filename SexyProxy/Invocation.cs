using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class Invocation
    {
        /// <summary>
        /// The instance upon which the method was invoked.
        /// </summary>
        public object Proxy { get; }

        /// <summary>
        /// The handler that is intercepting the call to the method
        /// </summary>
        public InvocationHandler InvocationHandler { get; }

        /// <summary>
        /// The method that was invoked that the InvocationHandler will handle
        /// </summary>
        public MethodInfo Method { get; }

        /// <summary>
        /// If the method represents a property accessor, then this returns the property for that method.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// The arguments to the method
        /// </summary>
        public object[] Arguments { get; }

        public abstract Task<object> Proceed();
        public abstract InvocationFlags Flags { get; }

        protected Invocation(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments)
        {
            Proxy = proxy;
            InvocationHandler = invocationHandler;
            Method = method;
            Property = property;
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

        public InvocationT(object proxy, InvocationHandler invocationHandler, MethodInfo method, PropertyInfo property, object[] arguments, Func<Invocation, T> implementation) : base(proxy, invocationHandler, method, property, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            return Task.FromResult<object>(implementation(this));
        }
    }
}
