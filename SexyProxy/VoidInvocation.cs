using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class VoidInvocation : Invocation
    {
        private Action<object[]> implementation;

        public VoidInvocation(MethodInfo method, object[] arguments, Action<object[]> implementation) : base(method, arguments)
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
