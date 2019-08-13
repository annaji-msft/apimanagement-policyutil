using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PolicyUtil
{
    public class MergeHelper
    {
        private static readonly Regex codeReplacementStartRegex = new Regex(@"^\s*@{\s*\$", RegexOptions.Compiled);
        private static readonly Regex codeReplacementRegex = new Regex(@"^\s*@{(?:\s*\$(?<action>[_a-z0-9]+\.[_a-z0-9]+);)*\s*\$(?<func>[_a-z0-9]+\.[_a-z0-9]+)\s*}\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void ProcessBatch(string rootDirectoryName)
        {
            // Expected folder structure
            // - Root
            //  - PolicyFolder1
            //      - markupfile
            //      - sourcecodefile
            //  - PolicyFolder2
            //      - markupfile
            //      - sourcecodefile
            foreach (var dir in System.IO.Directory.GetDirectories(rootDirectoryName))
            {
                string policyMarkupFile = Directory.GetFiles(dir, "*.xml")[0];
                string policySourceCodeFilePath = Directory.GetFiles(dir, "*.cs")[0];
                   
                ProcessSingle(policyMarkupFile, policySourceCodeFilePath);
            }
        }

        public static void ProcessSingle(string policyMarkupFilePath, string policySourceCodeFilePath)
        {
            try 
            {
                var originalPolicyXml = File.ReadAllText(policyMarkupFilePath);
                var xpolicyDoc = XDocument.Parse(originalPolicyXml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

                // Source-Code substitution should be done immediately, since the model for source code should
                // only depend upon the xml the author writes. It also allows us to ensure we can generate error
                // locations base on the original file line numbers.
                SubstitutePolicySourceCode(xpolicyDoc, policySourceCodeFilePath);

                // string output = HttpUtility.HtmlDecode(xpolicyDoc.ToString());

                var outputFilePath = Path.Combine(Path.GetDirectoryName(policyMarkupFilePath), Path.GetFileNameWithoutExtension(policyMarkupFilePath) + ".generated.policy.xml");

                xpolicyDoc.Save(outputFilePath);
            } 
            catch (Exception ex)
            {
                // add logger.
                Console.WriteLine("Could not merge files {0} and {1}", policyMarkupFilePath, policySourceCodeFilePath);
                Console.WriteLine(ex.Message);
            }
        }

        public static void SubstitutePolicySourceCode(XDocument xpolicyDocument, string policySourceCodeFilePath, bool enableSimpleExpressionDetection = false)
        {
            if (string.IsNullOrEmpty(policySourceCodeFilePath) || !File.Exists(policySourceCodeFilePath))
                return;

            SyntaxTree tree = ParseSourceCode(policySourceCodeFilePath);

            foreach (var xnode in xpolicyDocument.DescendantNodes())
            {
                switch (xnode.NodeType)
                {
                    case System.Xml.XmlNodeType.Element:
                        // Attributes are not considered XNodes, so we must iterate them separately.
                        foreach (var xattribute in ((XElement)xnode).Attributes())
                        {
                            SubstitutePolicySourceCode(xattribute, tree, enableSimpleExpressionDetection);
                        }
                        break;

                    // Note: XElement.Value gets processed as an XText node.
                    case System.Xml.XmlNodeType.Text:
                        SubstitutePolicySourceCode((XText)xnode, tree, enableSimpleExpressionDetection);
                        break;
                }
            }
        }

        public static SyntaxTree ParseSourceCode(string filePath)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
            var diagnostics = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning);

            if (diagnostics.Count() != 0)
            {
                string message = string.Empty;
                foreach (var error in diagnostics)
                {
                    message += filePath + error.ToString();
                }

                throw new InvalidOperationException(message);
            }

            PolicyValidator.ValidateAllowedTypes(filePath, tree);

            return tree;
        }

        private static void SubstitutePolicySourceCode(XAttribute xattribute, SyntaxTree tree, bool enableSimpleExpressionDetection)
        {
            string xnewValue;
            if (TrySubstitutePolicySourceCodeForNodeValue(xattribute.Value, tree, out xnewValue, enableSimpleExpressionDetection))
            {
                xattribute.Value = xnewValue;
            }
        }

        private static void SubstitutePolicySourceCode(XText xtext, SyntaxTree tree, bool enableSimpleExpressionDetection)
        {
            string xnewValue;
            if (TrySubstitutePolicySourceCodeForNodeValue(xtext.Value, tree, out xnewValue, enableSimpleExpressionDetection))
            {
                xtext.Value = xnewValue;
            }
        }

        /// <summary>
        /// Inspects an xml node's text value and inlines references to C# code-behind.
        /// Note, the input <paramref name="xnodeValue"/> and the output <paramref name="xnewNodeValue"/> are NOT xml encoded.
        /// </summary>
        /// <param name="xnodeValue">The string value for the xml node (this should be the unencoded text value).</param>
        /// <param name="tree"></param>
        /// <param name="xnewNodeValue">The new value for the node (not xml encoded) when this function returns true; otherwise, null.</param>
        /// <returns>true, if the node value is changed; otherwise, false. The expected usage is to allow callers to not have to modify the xml tree if no replacement is needed.</returns>
        public static bool TrySubstitutePolicySourceCodeForNodeValue(string xnodeValue, SyntaxTree tree, out string xnewNodeValue, bool enableSimpleExpressionDetection)
        {
            if (!codeReplacementStartRegex.IsMatch(xnodeValue))
            {
                xnewNodeValue = null;
                return false;
            }

            Match match = codeReplacementRegex.Match(xnodeValue);
            if (!match.Success)
            {
                throw new StopTransformException("Invalid Policy code fragment replacement expression: '" + xnodeValue + "'");
            }

            string result = string.Empty;

            // There may be zero or more <action> captures.
            foreach (Capture actionCapture in match.Groups["action"].Captures)
            {
                result += GetMinifiedMethodBodyCode(actionCapture.Value, tree);
            }

            // There should be exactly one <func> capture
            result += GetMinifiedMethodBodyCode(match.Groups["func"].Value, tree);

            if (enableSimpleExpressionDetection && result.StartsWith("return ") && result.EndsWith(";"))
            {
                xnewNodeValue = "@(" + result.Substring(7, result.Length - 8) + ")";
            }
            else
            {
                xnewNodeValue = "@{" + result + "}";
            }

            return true;
        }

        private static string GetMinifiedMethodBodyCode(string classNameDotMethodName, SyntaxTree tree)
        {
            BlockSyntax body = GetMethodBody(classNameDotMethodName, tree);
            WhitespaceMinimizationRewriter whitespaceMinimizer = new WhitespaceMinimizationRewriter();
            BlockSyntax noWhitespaceBody = (BlockSyntax)whitespaceMinimizer.Visit(body);
            string bodyCode = noWhitespaceBody.Statements.ToFullString();
            return bodyCode;
        }

        private static BlockSyntax GetMethodBody(string classNameDotMethodName, SyntaxTree tree)
        {
            string[] parts = classNameDotMethodName.Split('.');
            string className = parts[0];
            string methodName = parts[1];

            var classNode = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ToString() == className);
            if (classNode == null)
            {
                throw new StopTransformException("Unable to find the class '" + className + "' in " + tree.FilePath);
            }

            var methodNode = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ToString() == methodName);
            if (methodNode == null)
            {
                throw new StopTransformException("Unable to find the method '" + className + "." + methodName + "' in " + tree.FilePath);
            }

            return methodNode.Body;
        }
    }

    public class StopTransformException : Exception
    {
        public StopTransformException() : base() { }

        public StopTransformException(string message) : base(message) { }
    }
    public class WhitespaceMinimizationRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode Visit(SyntaxNode node)
        {
            SyntaxNode newNode = null;
            if (node != null)
            {
                newNode = node.WithoutLeadingTrivia();
            }

            return base.Visit(newNode);
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            SyntaxKind triviaKind = trivia.Kind();

            if (triviaKind == SyntaxKind.WhitespaceTrivia)
            {
                if (trivia.Span.Length == 1)
                {
                    return trivia;
                }

                SyntaxKind tokenKind = trivia.Token.Kind();
                if (tokenKind == SyntaxKind.CloseBraceToken)
                {
                    return SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "");
                }

                return SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ");
            }
            else if (triviaKind == SyntaxKind.EndOfLineTrivia ||
                triviaKind == SyntaxKind.SingleLineCommentTrivia ||
                triviaKind == SyntaxKind.MultiLineCommentTrivia)
            {
                return SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "");
            }

            return trivia;
        }
    }
}
