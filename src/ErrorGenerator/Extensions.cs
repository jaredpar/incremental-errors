
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ErrorGenerator;

internal static class Extensions
{
    public static void ReportDiagnostic(
        this SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        string filePath,
        TextSpan textSpan)
    {
        var compilationInfo = context.GetType().GetField("Compilation", BindingFlags.Instance | BindingFlags.NonPublic);
        var compilation = (Compilation)compilationInfo.GetValue(context);
        var syntaxTree = compilation.SyntaxTrees.Single(x => x.FilePath == filePath);
        var diagnostic = Diagnostic.Create(descriptor, Location.Create(syntaxTree, textSpan));
        context.ReportDiagnostic(diagnostic);
    }
}