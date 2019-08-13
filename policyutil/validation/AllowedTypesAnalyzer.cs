using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PolicyUtil
{
    public class AllowedTypesAnalyzer : DiagnosticAnalyzer
    {
        readonly Dictionary<string, string[]> allowedAssemblies;

        static readonly ImmutableArray<DiagnosticDescriptor> supportedDiagnostics = ImmutableArray.Create(
            CompilerDiagnosticConstants.TypeUsage,
            CompilerDiagnosticConstants.MethodUsage
        );

        readonly Dictionary<string, UsageConfig.MemberRule> allowedTypes;

        public AllowedTypesAnalyzer(Dictionary<string, UsageConfig.MemberRule> allowedTypes, Dictionary<string, string[]> allowedAssemblies)
        {
            this.allowedAssemblies = allowedAssemblies;

            this.allowedTypes = allowedTypes.ToDictionary(k =>
            {
                var assemblyQualifiedNameSeparator = k.Key.IndexOf(",");
                var typeName = assemblyQualifiedNameSeparator > -1 ? k.Key.Substring(0, assemblyQualifiedNameSeparator) : k.Key;
                var genericSeparator = typeName.LastIndexOf("`");
                return genericSeparator > -1 ? typeName.Substring(0, genericSeparator) : typeName;
            },
            k => k.Value);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                this.AnalyzeNode,
                SyntaxKind.InvocationExpression,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.ElementAccessExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ObjectInitializerExpression,
                SyntaxKind.AnonymousObjectCreationExpression
            );
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return supportedDiagnostics; }
        }

        void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var symbolInfo = context.SemanticModel.GetSymbolInfo(node);
            var memberSymbol = symbolInfo.Symbol;
            if (memberSymbol == null)
            {
                if (symbolInfo.CandidateReason == CandidateReason.LateBound)
                {
                    context.ReportDiagnostic(Diagnostic.Create(CompilerDiagnosticConstants.LateBoundUsage, node.GetLocation()));
                }
                return;
            }

            if (memberSymbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.LocalFunction)
            {
                return;
            }

            if (memberSymbol.ContainingSymbol.Kind == SymbolKind.Namespace)
            {
                return;
            }

            var typeSymbol = memberSymbol.ContainingSymbol;

            if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsAnonymousType)
            {
                return;
            }

            typeSymbol = IsNullableType(typeSymbol) ? typeSymbol.OriginalDefinition : typeSymbol;
            var typeName = typeSymbol.ToDisplayString(CompilerDiagnosticConstants.TypeDisplayFormat);

            var assemblySymbol = typeSymbol.ContainingAssembly;

            var assemblyAllowed = this.allowedAssemblies.TryGetValue(assemblySymbol.Identity.GetDisplayName(), out var allowedNamespaces)
                && (allowedNamespaces.Contains("*") || allowedNamespaces.Contains(typeSymbol.ContainingNamespace.ToDisplayString()));

            var typeAllowedExplicitly = this.allowedTypes.TryGetValue(typeName, out var memberRule);
            var typeAllowed = assemblyAllowed || typeAllowedExplicitly;

            if (!typeAllowed)
            {
                context.ReportDiagnostic(Diagnostic.Create(CompilerDiagnosticConstants.TypeUsage, node.GetLocation(), typeSymbol.ToDisplayString(CompilerDiagnosticConstants.TypeDisplayFormat)));
                return;
            }

            if (!typeAllowedExplicitly)
            {
                return;
            }

            var memberDenied = memberRule.Deny != null && memberRule.Deny.Contains(memberSymbol.MetadataName);
            var memberAllowed = memberRule.Allow != null && (memberRule.Allow.Contains("*") || memberRule.Allow.Contains(memberSymbol.MetadataName));
            if (!memberDenied && memberAllowed)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(CompilerDiagnosticConstants.MethodUsage, node.GetLocation(), memberSymbol.Name, typeSymbol.ToDisplayString(CompilerDiagnosticConstants.TypeDisplayFormat)));
        }

        static bool IsNullableType(ISymbol symbol)
        {
            var namedSymbol = symbol as INamedTypeSymbol;
            return namedSymbol != null && namedSymbol.Arity == 1 && namedSymbol.ToDisplayString(CompilerDiagnosticConstants.TypeDisplayFormat).EndsWith("?");
        }
    }
}