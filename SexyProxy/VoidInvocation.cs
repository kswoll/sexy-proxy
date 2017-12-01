using System;
using System.Reflection;

namespace SexyProxy
{
    public class VoidInvocation : Invocation
    {
        public override InvocationFlags Flags => InvocationFlags.Void;

        private Action<Invocation> implementation;

        public VoidInvocation(object proxy, MethodInfo method, PropertyInfo property, object[] arguments, Action<Invocation> implementation) : base(proxy, method, property, arguments)
        {
            this.implementation = implementation;
        }

        public override object Proceed()
        {
            implementation(this);
            return null;
        }
    }
}
