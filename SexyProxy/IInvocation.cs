using System.Reflection;

namespace SexyProxy
{
    public interface IInvocation
    {
        /// <summary>
        /// The instance upon which the method was invoked.
        /// </summary>
        object Proxy { get; }

        /// <summary>
        /// The method that was invoked that the InvocationHandler will handle
        /// </summary>
        MethodInfo Method { get; }

        /// <summary>
        /// If the method represents a property accessor, then this returns the property for that method.
        /// </summary>
        PropertyInfo Property { get; }

        /// <summary>
        /// The arguments to the method
        /// </summary>
        object[] Arguments { get; }

        InvocationFlags Flags { get; }
    }
}
