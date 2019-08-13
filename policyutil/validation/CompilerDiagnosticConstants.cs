using Microsoft.CodeAnalysis;

namespace PolicyUtil
{
    public class CompilerDiagnosticConstants
    {
        public const string CompilerDiagnosticsCategory = "Microsoft.Azure.ApiManagement.Policies.Expressions";

        public static readonly SymbolDisplayFormat TypeDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        public static readonly DiagnosticDescriptor TypeUsage
            = new DiagnosticDescriptor("APIM0001", "Type is not supported", "Usage of type '{0}' is not supported within expressions", CompilerDiagnosticsCategory, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor MethodUsage
            = new DiagnosticDescriptor("APIM0002", "Method is not supported", "Usage of member '{0}' of type '{1}' is not supported within expressions", CompilerDiagnosticsCategory, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor LateBoundUsage
           = new DiagnosticDescriptor("APIM0006", "Late binding is not supported", "Dynamic member invocation is not supported", CompilerDiagnosticsCategory, DiagnosticSeverity.Error, true);
    }
}
