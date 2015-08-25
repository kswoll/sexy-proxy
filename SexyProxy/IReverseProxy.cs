namespace SexyProxy
{
    public interface IReverseProxy
    {
        InvocationHandler InvocationHandler { get; }
    }
}