using Dwarf.Toolkit.Maui.SourceGenerators.Constants;
using Dwarf.Toolkit.Maui.SourceGenerators.Models;
using Dwarf.Toolkit.SourceGenerators.Extensions;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Dwarf.Toolkit.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace Dwarf.Toolkit.Maui.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public sealed partial class AttachedPropertyGenerator : IIncrementalGenerator
{
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Gather info for all annotated fields
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, Result<AttachedPropertyInfo?> Info)> propertyInfoWithErrors
			= context.SyntaxProvider.ForAttributeWithMetadataName(
				BindableAttributeNaming.AttachedFullyQualifiedName,
				Execute.IsCandidateMethodDeclaration,
				static (context, token) =>
				{
					// Validate that the candidate is valid for the current compilation
					// and the symbol as well before doing any work
					if (!Execute.IsCandidateValidForCompilation(context.SemanticModel)
						|| !Execute.IsCandidateSymbolValid(context.TargetSymbol))
					{
						return default;
					}

					token.ThrowIfCancellationRequested();

					// Get the hierarchy info for the target symbol, and try to gather the property info
					HierarchyInfo hierarchy = HierarchyInfo.From(context.TargetSymbol.ContainingType);

					token.ThrowIfCancellationRequested();

					_ = Execute.TryGetInfo(
						context,
						token,
						out AttachedPropertyInfo? propertyInfo,
						out ImmutableArray<DiagnosticInfo> diagnostics);

					token.ThrowIfCancellationRequested();

					return (Hierarchy: hierarchy, new Result<AttachedPropertyInfo?>(propertyInfo, diagnostics));
				})
			.Where(static item => item.Hierarchy is not null);

		// Output the diagnostics
		context.ReportDiagnostics(propertyInfoWithErrors.Select(static (item, _) => item.Info.Errors));

		// Get the filtered sequence to enable caching
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, Result<AttachedPropertyInfo> Info)> propertyInfo
			= propertyInfoWithErrors.Where(static item => item.Info.Value is not null)!;

		// Split and group by containing type
		IncrementalValuesProvider<(HierarchyInfo Hierarchy, EquatableArray<AttachedPropertyInfo> Properties)> groupedPropertyInfo
			= propertyInfo.GroupBy(static item => item.Left, static item => item.Right.Value);

		// Generate the requested properties and methods
		context.RegisterSourceOutput(groupedPropertyInfo, static (context, item) =>
		{
			// Generate all member declarations for the current type
			ImmutableArray<MemberDeclarationSyntax> memberDeclarations
				= item.Properties
					.SelectMany(p => Execute.GetPropertySyntax(item.Hierarchy, p))
					.Concat(item.Properties.SelectMany(p => Execute.GetOnPropertyChangeMethodsSyntax(item.Hierarchy, p)))
					.ToImmutableArray();

			// Insert all members into the same partial type declaration
			CompilationUnitSyntax compilationUnit = item.Hierarchy.GetCompilationUnit(memberDeclarations);

			context.AddSource($"{item.Hierarchy.FilenameHint}.g.cs", compilationUnit);
		});
	}
}