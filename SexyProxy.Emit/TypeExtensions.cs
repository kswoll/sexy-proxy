using System;
using System.Threading.Tasks;

namespace SexyProxy.Emit
{
    public static class TypeExtensions
    {
        public static bool IsTaskT(this Type type)
        {
            var current = type;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Task<>))
                    return true;
                current = current.BaseType;
            }
            return false;
        }

        public static Type GetTaskType(this Type type)
        {
            var current = type;
            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Task<>))
                    return current.GetGenericArguments()[0];
                current = current.BaseType;                
            }
            throw new Exception("Type " + type.FullName + " is not an instance of Task<T>");
        }
    }
}
