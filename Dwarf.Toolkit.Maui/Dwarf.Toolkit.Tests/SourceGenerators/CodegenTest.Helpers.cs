﻿using NUnit.Framework.Legacy;
using NUnit.Framework;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dwarf.Toolkit.Tests.SourceGenerators;

partial class CodegenTest
{
	/// <summary>
	/// Generates the requested sources
	/// </summary>
	/// <param name="source">The input source to process.</param>
	/// <param name="generators">The generators to apply to the input syntax tree.</param>
	/// <param name="results">The source files to compare.</param>
	private static void VerifyGenerateSources(string source, IIncrementalGenerator[] generators, params (string filename, string? text)[] results)
	{
		VerifyGenerateSources(source, generators, LanguageVersion.CSharp10, results);
	}

	/// <summary>
	/// Generates the requested sources
	/// </summary>
	/// <param name="source">The input source to process.</param>
	/// <param name="generators">The generators to apply to the input syntax tree.</param>
	/// <param name="languageVersion">The language version to use.</param>
	/// <param name="results">The source files to compare.</param>
	private static void VerifyGenerateSources(string source, IIncrementalGenerator[] generators, LanguageVersion languageVersion, params (string filename, string? text)[] results)
	{
		// Ensure CommunityToolkit.Mvvm and System.ComponentModel.DataAnnotations are loaded
		//Type observableObjectType = typeof(ObservableObject);
		//Type validationAttributeType = typeof(ValidationAttribute);

		// Get all assembly references for the loaded assemblies (easy way to pull in all necessary dependencies)
		IEnumerable<MetadataReference> references =
			from assembly in AppDomain.CurrentDomain.GetAssemblies()
			where !assembly.IsDynamic
			let reference = MetadataReference.CreateFromFile(assembly.Location)
			select reference;

		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(languageVersion));

		// Create a syntax tree with the input source
		CSharpCompilation compilation = CSharpCompilation.Create(
			"original",
			new SyntaxTree[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators).WithUpdatedParseOptions((CSharpParseOptions)syntaxTree.Options);

		// Run all source generators on the input source code
		_ = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

		// Ensure that no diagnostics were generated
		CollectionAssert.AreEquivalent(Array.Empty<Diagnostic>(), diagnostics);

		foreach ((string filename, string? text) in results)
		{
			if (text is not null)
			{
				string filePath = filename;

				// Update the assembly version using the version from the assembly of the input generators.
				// This allows the tests to not need updates whenever the version of the MVVM Toolkit changes.
				string expectedText = text.Replace("<ASSEMBLY_VERSION>", $"\"{generators[0].GetType().Assembly.GetName().Version}\"");
				SyntaxTree generatedTree = outputCompilation.SyntaxTrees.Single(tree => Path.GetFileName(tree.FilePath) == filePath);

				//Assert.AreEqual(expectedText, generatedTree.ToString());
				Assert.That(expectedText, Is.EqualTo(generatedTree.ToString()));
			}
			else
			{
				// If the text is null, verify that the file was not generated at all
				//Assert.IsFalse(outputCompilation.SyntaxTrees.Any(tree => Path.GetFileName(tree.FilePath) == filename));
				Assert.That(outputCompilation.SyntaxTrees.Any(tree => Path.GetFileName(tree.FilePath) == filename), Is.False);
			}
		}

		//GC.KeepAlive(observableObjectType);
		//GC.KeepAlive(validationAttributeType);
	}
}