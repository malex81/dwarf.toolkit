using Dwarf.Toolkit.Maui;
using Dwarf.Toolkit.Maui.SourceGenerators;
using Dwarf.Toolkit.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Maui.Controls;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Dwarf.Toolkit.Tests.Analyzers;

/*
 * https://www.google.com/search?sourceid=chrome&aep=42&source=chrome.crn.rb&udm=50&q=%D0%9A%D0%B0%D0%BA+%D0%B8%D1%81%D0%BF%D0%BE%D0%BB%D1%8C%D0%B7%D0%BE%D0%B2%D0%B0%D1%82%D1%8C+CSharpAnalyzerTest&mstk=AUtExfD8S6i4gL59Bpd__m9i2kPEyUEBvNiYBfx2p_zYw4ao1NA2Iw81iwPR1DQhEu1vP7xlVJ1GW3Ze1zA062VTm1zQPZEKCfVLtkco92l5-iSC_Nf-SG2_U1DQrHA2U0d5_1vy2zsNi05InrPHkzCvFAp2_lK74-4k6nxx4f35iZEfMKyuKNjSCKSBpHTZp47C80dZm4_4aV5Wq5jEs6vOhLxOgDPrwrFGRf8o4Aah3G8mILWB_ql2kRZeNYQd2iGr0Ysqhd7GXQZUF-aVCwuIZ6bTtib83Jqq4q0&csuir=1&mtid=VB_IaczFLZ2hwPAPo7m8uQw
 *
 */

internal static class CodegenTestHelpers
{
	internal static async Task VerifyAnalyzerDiagnosticsAndSuccessfulGeneration<TAnalyzer>(string markdownSource)
	   where TAnalyzer : DiagnosticAnalyzer, new()
	{
		await VerifyAnalyzerDiagnosticsAndSuccessfulGeneration<TAnalyzer>(markdownSource, [], []);
	}

	/// <summary>
	/// Verifies the diagnostic errors for a given analyzer, and that all available source generators can run successfully with the input source (including subsequent compilation).
	/// </summary>
	/// <typeparam name="TAnalyzer">The type of the analyzer to test.</typeparam>
	/// <param name="markdownSource">The input source to process with diagnostic annotations.</param>
	/// <param name="languageVersion">The language version to use to parse code and run tests.</param>
	/// <param name="generatorDiagnosticsIds">The diagnostic ids to expect for the input source code.</param>
	/// <param name="ignoredDiagnosticIds">The list of diagnostic ids to ignore in the final compilation.</param>
	internal static async Task VerifyAnalyzerDiagnosticsAndSuccessfulGeneration<TAnalyzer>(string markdownSource, string[] generatorDiagnosticsIds, string[] ignoredDiagnosticIds)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		// .Net 8 - C# 12
		// .Net 9 - C# 13 - partial properties were introduced
		var languageVersion = LanguageVersion.CSharp13;
		await CSharpAnalyzerWithLanguageVersionTest<TAnalyzer>.VerifyAnalyzerAsync(markdownSource, languageVersion);

		IIncrementalGenerator[] generators =
		[
			new BindablePropertyGenerator(),
			new AttachedPropertyGenerator()
		];

		// Transform diagnostic annotations back to normal C# (eg. "{|MVVMTK0008:Foo()|}" ---> "Foo()")
		string source = Regex.Replace(markdownSource, @"{\|((?:,?\w+)+):(.+?)\|}", m => m.Groups[2].Value);

		VerifyGeneratedDiagnostics(CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(languageVersion)), generators, generatorDiagnosticsIds, ignoredDiagnosticIds);
	}

	/// <summary>
	/// Verifies the output of one or more source generators.
	/// </summary>
	/// <param name="syntaxTree">The input source tree to process.</param>
	/// <param name="generators">The generators to apply to the input syntax tree.</param>
	/// <param name="generatorDiagnosticsIds">The diagnostic ids to expect for the input source code.</param>
	/// <param name="ignoredDiagnosticIds">The list of diagnostic ids to ignore in the final compilation.</param>
	internal static void VerifyGeneratedDiagnostics(SyntaxTree syntaxTree, IIncrementalGenerator[] generators, string[] generatorDiagnosticsIds, string[] ignoredDiagnosticIds)
	{
		Type bindableObjectType = typeof(BindableObject);
		Type bindableAttributeType = typeof(BindablePropertyAttribute);

		// Get all assembly references for the loaded assemblies (easy way to pull in all necessary dependencies)
		IEnumerable<MetadataReference> references =
			from assembly in AppDomain.CurrentDomain.GetAssemblies()
			where !assembly.IsDynamic
			let reference = MetadataReference.CreateFromFile(assembly.Location)
			select reference;

		// Create a syntax tree with the input source
		CSharpCompilation compilation = CSharpCompilation.Create(
			"original",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generators).WithUpdatedParseOptions((CSharpParseOptions)syntaxTree.Options);

		// Run all source generators on the input source code
		_ = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> diagnostics);

		string[] resultingIds = diagnostics.Select(diagnostic => diagnostic.Id).ToArray();

		CollectionAssert.AreEquivalent(generatorDiagnosticsIds, resultingIds, $"resultingIds: {string.Join(", ", resultingIds)}");

		// If the compilation was supposed to succeed, ensure that no further errors were generated
		if (resultingIds.Length == 0)
		{
			// Compute diagnostics for the final compiled output (just include errors)
			List<Diagnostic> outputCompilationDiagnostics = outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();

			// Filtered diagnostics
			List<Diagnostic> filteredDiagnostics = outputCompilationDiagnostics.Where(diagnostic => !ignoredDiagnosticIds.Contains(diagnostic.Id)).ToList();

			Assert.That(filteredDiagnostics.Count == 0, Is.True, $"resultingIds: {string.Join(", ", filteredDiagnostics)}");
		}

		GC.KeepAlive(bindableObjectType);
		GC.KeepAlive(bindableAttributeType);
	}
}