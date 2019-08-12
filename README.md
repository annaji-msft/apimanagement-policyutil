# apimanagement-policyutil 
Experimental - Azure API Management Policy Utility

This is a experimental project to move the policy expression outside of the markup. That would improve testability of the policy expressions, enable use of C# tooling as well as build tools to perform static analysis on policy expressions.

Clone the repo and open in vscode 

# PolicyLib
Contains classes built from the documentation here
https://docs.microsoft.com/en-us/azure/api-management/api-management-policy-expressions#ContextVariables

# PolicyUtil
Merges the policy xml file and cs file. Exposes a option to perform batch opertaion (expect a specific folder structure).

# SamplePolicyProject
Contains templated policy markup file (PolicyMarkupFile.xml) and policyexpression csharp file(PolicySourceCode.cs)

To reference a expression in the markup the format used is
@{$CSFile.MethodName} example @{$PolicySourceCode.GetAuthHeaderValue}

Press F5 that would generate
PolicyMarkupFile.generated.policy.xml file combining PolicyMarkupFile.xml and PolicySourceCode.cs

# SamplePolicyProjectTests
Tests for the PolicySourceCode.cs

# Other Notes
- Currently does not support inheritance. That had the potential to reuse expressions. Needs update to the merge util.
- Since the expressions are moved to a csharp file, can write roslyn analyzers to validation the expressions at runtime. Example, make sure other supported .net types and newtonsoft types are used in the expression and supported context variable members



