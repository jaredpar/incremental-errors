using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace ErrorGenerator.UnitTests;

public class ErrorGeneratorTests
{
    private GeneratorDriver? GeneratorDriver;

    private Compilation CreateCompilation(IEnumerable<(string Source, string FilePath)> sources)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        var compilation = CSharpCompilation
            .Create(
                assemblyName: "TestAssembly",
                syntaxTrees: sources.Select(x => CSharpSyntaxTree.ParseText(x.Source, path: x.FilePath)),
                options: options)
            .WithReferences(Net80.References.All);

        return compilation;
    }

    private GeneratorDriverRunResult Run(string source, string filePath)
    {
        if (GeneratorDriver is null)
        {
            GeneratorDriver = CSharpGeneratorDriver.Create(new ErrorGenerator());
        }

        var compilation = CreateCompilation([(source, filePath)]);
        GeneratorDriver = GeneratorDriver.RunGenerators(compilation);
        return GeneratorDriver.GetRunResult();
    }

    private void VerifyDiagnostics(
        (string FilePath, int Start, string? Message)[] expected,
        ImmutableArray<Diagnostic> actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            var (expectedFilePath, expectedStart, expectedMessage) = expected[i];
            var diagnostic = actual[i];
            Assert.Equal(expectedFilePath, diagnostic.Location.SourceTree?.FilePath);

            var expectedTextSpan = new TextSpan(expectedStart, 1);
            Assert.Equal(expectedTextSpan, diagnostic.Location.SourceSpan);

            if (expectedMessage is not null)
            {
                Assert.Equal(expectedMessage, diagnostic.GetMessage());
            }
        }
    }

    [Fact]
    public void Simple()
    {
        var result = Run("""
            [Error("Hello?")]
            class C { }
            """, "file.cs");

        VerifyDiagnostics(
            [
                ("file.cs", 0, @"Illegal character '?' in message ""Hello?"""),
            ], result.Diagnostics);

        result = Run("""
            [Error("Hello??")]
            class C { }
            """, "file.cs");
        VerifyDiagnostics(
            [
                ("file.cs", 0, null),
                ("file.cs", 1, null),
            ], result.Diagnostics);

        result = Run("""
            [Error("Hello")]
            class C { }
            """, "file.cs");
        VerifyDiagnostics([], result.Diagnostics);
    }

    [Fact]
    public void Boundary()
    {
        var code = """
            // See https://aka.ms/new-console-template for more information
            using System;

            Console.WriteLine("Hello, World!");

            [Error("hello???")]
            class C { }
            """;
        var result = Run(code, "program.cs");
        Assert.NotEmpty(result.Diagnostics);
    }
}
