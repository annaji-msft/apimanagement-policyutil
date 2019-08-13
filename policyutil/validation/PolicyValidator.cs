using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Threading;
using System.Collections.Immutable;
using System.Reflection;
using Newtonsoft.Json;

namespace PolicyUtil
{

    public static class PolicyValidator
    {
        public static void ValidateAllowedTypes(string codefile, SyntaxTree tree)
        {
            var json = GetEmbeddedResource("validation/expression.json", Assembly.GetExecutingAssembly());
            var usageConfig = JsonConvert.DeserializeObject<UsageConfig>(json);

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
               new AllowedTypesAnalyzer(usageConfig.AllowedUsageTypes, usageConfig.AllowedUsageAssemblies)
           );

            var assemblies = new List<Assembly>();
            foreach (var reference in usageConfig.References)
            {
                assemblies.Add(Assembly.Load(reference));
            }

            var references = assemblies.Select(a => MetadataReference.CreateFromFile(a.Location)).ToArray();

            var compilation = CSharpCompilation.Create("APIMPolicy")
                .AddReferences(references)
                .AddSyntaxTrees(tree);

            var diagnostics = compilation
                 .WithAnalyzers(analyzers)
                 .GetAnalyzerDiagnosticsAsync(CancellationToken.None)
                 .Result;

            if (diagnostics.Count() != 0)
            {
                string message = string.Empty;
                foreach (var error in diagnostics)
                {
                    message += codefile + error.ToString();
                }

                throw new InvalidOperationException(message);
            }
        }

        private static string GetEmbeddedResource(string resourceName, Assembly assembly)
        {
            resourceName = FormatResourceName(assembly, resourceName);
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    return null;

                using (StreamReader reader = new StreamReader(resourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string FormatResourceName(Assembly assembly, string resourceName)
        {
            return assembly.GetName().Name + "." + resourceName.Replace(" ", "_")
                                                               .Replace("\\", ".")
                                                               .Replace("/", ".");
        }
    }
}