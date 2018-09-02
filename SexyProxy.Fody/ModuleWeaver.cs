using System;
using System.Collections.Generic;
using Fody;
using Mono.Cecil;

namespace SexyProxy.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public override IEnumerable<string> GetAssembliesForScanning()
        {
            return new[] { "netstandard", "mscorlib" };
        }

        public override void Execute()
        {
            CecilExtensions.LogError = LogError;
            CecilExtensions.LogInfo = LogInfo;
            CecilExtensions.LogWarning = LogWarning;
            CecilExtensions.Initialize(ModuleDefinition);

            var propertyWeaver = new SexyProxyWeaver
            {
                ModuleDefinition = ModuleDefinition,
                LogInfo = LogInfo,
                LogWarning = LogWarning,
                LogError = LogError
            };
            propertyWeaver.Execute();
        }
    }
}