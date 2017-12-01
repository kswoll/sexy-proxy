using System.Threading.Tasks;

namespace SexyProxy
{
    public interface IAsyncInvocation : IInvocation
    {
        Task<object> Proceed();
    }
}
