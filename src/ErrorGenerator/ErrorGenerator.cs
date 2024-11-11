using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ErrorData = (string FileName, Microsoft.CodeAnalysis.Text.TextSpan TextSpan, string Argument, int Position);

#pragma warning disable RS2008 // Enable analyzer release tracking
namespace ErrorGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class ErrorGenerator : IIncrementalGenerator
{
    public static readonly DiagnosticDescriptor ErrorInAttribute =
        new DiagnosticDescriptor(
            "ESG0001",
            "Illegal character in message",
            @"Illegal character '{0}' in message ""{1}""",
            nameof(ErrorGenerator),
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(context =>
        {
            var source = """
                using System;
                internal sealed class ErrorAttribute : Attribute
                {
                    public ErrorAttribute(string message) => Message = message;
                    public string Message { get; }
                }   
                """;
            var sourceText = SourceText.From(source, Encoding.UTF8);
            context.AddSource("ErrorAttribute.cs", sourceText);
        });

        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "ErrorAttribute",
                (_, _) => true,
                GetErrorModel);

        context.RegisterSourceOutput(results, ProduceDiagnostics);

        static ErrorModel GetErrorModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var list = new List<ErrorData>();
            foreach (var attributeData in context.Attributes)
            {
                if (attributeData.ConstructorArguments.Length == 1 &&
                    attributeData.ConstructorArguments[0].Value is string message)
                {
                    int count = 0;
                    for (int i = 0; i < message.Length; i++)
                    {
                        if (message[i] is '?')
                        {
                            var span = new TextSpan(context.TargetNode.Span.Start + count, 1);
                            count++;
                            list.Add((context.TargetNode.SyntaxTree.FilePath, span, message, i));
                        }
                    }
                }
            }

            return new ErrorModel(list.ToArray());
        }

        static void ProduceDiagnostics(SourceProductionContext context, ErrorModel model)
        {
            foreach (var error in model.Errors)
            {
                context.ReportDiagnostic(
                    ErrorInAttribute,
                    error.FileName,
                    error.TextSpan,
                    error.Argument[error.Position],
                    error.Argument);
            }
        }
    }
}

public sealed class ErrorModel : IEquatable<ErrorModel>
{
    public ErrorData[] Errors { get; }
    public int Count => Errors.Length;

    public ErrorModel(ErrorData[] errors)
    {
        Errors = errors;
    }

    public override bool Equals(object obj) => obj is ErrorModel other && Equals(other);

    public override int GetHashCode() => Errors.Length;

    public bool Equals(ErrorModel other)
    {
        if (Count != other.Count)
        {
            return false;
        }

        for (int i = 0; i < Count; i++)
        {
            if (Errors[i] != other.Errors[i])
            {
                return false;
            }
        }

        return true;
    }

}
