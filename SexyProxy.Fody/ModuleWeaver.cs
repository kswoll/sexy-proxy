using System;
using Mono.Cecil;

namespace SexyProxy.Fody
{
    public class ModuleWeaver
    {
        public ModuleDefinition ModuleDefinition { get; set; }

        // Will log an MessageImportance.High message to MSBuild. 
        public Action<string> LogInfo  { get; set; }

        // Will log an error message to MSBuild. OPTIONAL
        public Action<string> LogError { get; set; }

        public Action<string> LogWarning { get; set; }

        public void Execute()
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