using System;

namespace SexyProxy
{
    [AttributeUsage(AttributeTargets.Method)]
    public class OriginalMethodAttribute : Attribute
    {
        public string Name { get; }

        public OriginalMethodAttribute(string name)
        {
            Name = name;
        }
    }
}
