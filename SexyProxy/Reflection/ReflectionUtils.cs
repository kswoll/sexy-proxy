using System.Linq;
using System.Reflection;

namespace SexyProxy.Reflection
{
    public static class ReflectionUtils
    {
        public static PropertyInfo GetProperty(this MethodInfo method)
        {
            var hasReturn = method.ReturnType != typeof(void);
            if (!hasReturn)
            {
                return method.DeclaringType.GetProperties().FirstOrDefault(prop => prop.GetSetMethod() == method);
            }
            else
            {
                return method.DeclaringType.GetProperties().FirstOrDefault(prop => prop.GetGetMethod() == method);
            }
        }
    }
}
