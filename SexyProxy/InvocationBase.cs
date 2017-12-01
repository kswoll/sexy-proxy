using System.Reflection;

namespace SexyProxy
{
    public abstract class InvocationBase
    {
        /// <summary>
        /// The instance upon which the method was invoked.
        /// </summary>
        public object Proxy { get; }

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

        public abstract InvocationFlags Flags { get; }

        protected InvocationBase(object proxy, MethodInfo method, PropertyInfo property, object[] arguments)
        {
            Proxy = proxy;
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
}
