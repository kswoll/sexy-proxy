using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public abstract class Invocation
    {
        public MethodInfo Method { get; private set; }
        public object[] Arguments { get; set; }        

        public abstract Task<object> Proceed();

        protected Invocation(MethodInfo method, object[] arguments)
        {
            Method = method;
            Arguments = arguments;
        }
    }

    public class InvocationT<T> : Invocation
    {
        private Func<object[], T> implementation;

        public InvocationT(MethodInfo method, object[] arguments, Func<object[], T> implementation) : base(method, arguments)
        {
            this.implementation = implementation;
        }

        public override Task<object> Proceed()
        {
            return Task.FromResult<object>(implementation(Arguments));
        }
    }
}
