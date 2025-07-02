using Dwarf.Toolkit.Maui.SourceGenerators.Extensions;
using Dwarf.Toolkit.Maui.SourceGenerators.Helpers;
using Dwarf.Toolkit.Maui.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Dwarf.Toolkit.Maui.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed partial class BindablePropertyGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Gather info for all annotated fields
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, Result<PropertyInfo?> Info)> propertyInfoWithErrors
			= context.ForAttributeWithMetadataNameAndOptions(
				"Dwarf.Toolkit.Maui.BindablePropertyAttribute",
				Execute.IsCandidatePropertyDeclaration,
				static (context, token) =>
				{
					MemberDeclarationSyntax memberSyntax = Execute.GetCandidateMemberDeclaration(context.TargetNode);

					// Validate that the candidate is valid for the current compilation
					if (!Execute.IsCandidateValidForCompilation(memberSyntax, context.SemanticModel))
					{
						return default;
					}

					// Validate the symbol as well before doing any work
					if (!Execute.IsCandidateSymbolValid(context.TargetSymbol))
					{
						return default;
					}

					token.ThrowIfCancellationRequested();

					// Get the hierarchy info for the target symbol, and try to gather the property info
					HierarchyInfo hierarchy = HierarchyInfo.From(context.TargetSymbol.ContainingType);

					token.ThrowIfCancellationRequested();

					_ = Execute.TryGetInfo(
						memberSyntax,
						context.TargetSymbol,
						context.SemanticModel,
						context.GlobalOptions,
						token,
						out PropertyInfo? propertyInfo,
						out ImmutableArray<DiagnosticInfo> diagnostics);

					token.ThrowIfCancellationRequested();

					return (Hierarchy: hierarchy, new Result<PropertyInfo?>(propertyInfo, diagnostics));
				})
			.Where(static item => item.Hierarchy is not null);

		// Output the diagnostics
		context.ReportDiagnostics(propertyInfoWithErrors.Select(static (item, _) => item.Info.Errors));

		// Get the filtered sequence to enable caching
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, Result<PropertyInfo> Info)> propertyInfo
			= propertyInfoWithErrors.Where(static item => item.Info.Value is not null)!;

		// Split and group by containing type
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, EquatableArray<PropertyInfo> Properties)> groupedPropertyInfo
			= propertyInfo.GroupBy(static item => item.Left, static item => item.Right.Value);

		// Generate the requested properties and methods
		context.RegisterSourceOutput(groupedPropertyInfo, static (context, item) =>
		{
			// Generate all member declarations for the current type
			ImmutableArray<MemberDeclarationSyntax> memberDeclarations
				= item.Properties
					.Select(Execute.GetPropertySyntax)
					.Concat(item.Properties.Select(Execute.GetOnPropertyChangeMethodsSyntax).SelectMany(static l => l))
					.ToImmutableArray();

			// Insert all members into the same partial type declaration
			CompilationUnitSyntax compilationUnit = item.Hierarchy.GetCompilationUnit(memberDeclarations);

			context.AddSource($"{item.Hierarchy.FilenameHint}.g.cs", compilationUnit);
		});
	}
}