using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SexyProxy.Reflection
{
    public static class MethodFinder<T>
    {
        private static Dictionary<string, MethodInfo> methodsBySignature = new Dictionary<string, MethodInfo>();

        static MethodFinder()
        {
            foreach (var method in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var signature = GenerateSignature(method);
                methodsBySignature[signature] = method;
            }
        }

        private static string GenerateSignature(MethodInfo method)
        {
            return $"{method.Name}$$${method.GetGenericArguments().Length}$$$" +
                   $"{string.Join("$$", method.GetParameters().Select(x => x.ParameterType.FullName.Replace(".", "$")))}";
        }

        public static MethodInfo FindMethod(string signature)
        {
            return methodsBySignature[signature];
        }
    }
}
