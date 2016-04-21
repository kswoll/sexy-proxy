using System.Reflection;

namespace SexyProxy
{
    public delegate bool ProxyPredicate<in T>(T target, MethodInfo method, PropertyInfo property);
}
