using Dwarf.Toolkit.Maui.SourceGenerators.Models;
using Dwarf.Toolkit.SourceGenerators.Extensions;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using static Microsoft.CodeAnalysis.SymbolDisplayTypeQualificationStyle;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dwarf.Toolkit.SourceGenerators.Models;

/// <summary>
/// A model describing the hierarchy info for a specific type.
/// </summary>
/// <param name="FilenameHint">The filename hint for the current type.</param>
/// <param name="MetadataName">The metadata name for the current type.</param>
/// <param name="Namespace">Gets the namespace for the current type.</param>
/// <param name="Hierarchy">Gets the sequence of type definitions containing the current type.</param>
internal sealed partial record HierarchyInfo(string FilenameHint, INamedTypeSymbol ClassSymbol, string Namespace, EquatableArray<TypeInfo> Hierarchy)
{
	/// <summary>
	/// Creates a new <see cref="HierarchyInfo"/> instance from a given <see cref="INamedTypeSymbol"/>.
	/// </summary>
	/// <param name="typeSymbol">The input <see cref="INamedTypeSymbol"/> instance to gather info for.</param>
	/// <returns>A <see cref="HierarchyInfo"/> instance describing <paramref name="typeSymbol"/>.</returns>
	public static HierarchyInfo From(INamedTypeSymbol typeSymbol)
	{
		using ImmutableArrayBuilder<TypeInfo> hierarchy = ImmutableArrayBuilder<TypeInfo>.Rent();

		for (INamedTypeSymbol? parent = typeSymbol; parent is not null; parent = parent.ContainingType)
		{
			hierarchy.Add(new TypeInfo(
				parent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
				parent.TypeKind,
				parent.GetModifiers().ContainsAnyAccessibilityModifiers() ? parent.DeclaredAccessibility : Accessibility.NotApplicable,
				//GetTypeModifiers(parent).AsUnderlyingType(),
				parent.IsRecord));
		}

		return new(
			typeSymbol.GetFullyQualifiedMetadataName(),
			typeSymbol.OriginalDefinition,
			typeSymbol.ContainingNamespace.ToDisplayString(new(typeQualificationStyle: NameAndContainingTypesAndNamespaces)),
			hierarchy.ToImmutable());
	}

	//private static ImmutableArray<SyntaxKind> GetTypeModifiers(INamedTypeSymbol typeSymbol)
	//{
	//	ReadOnlySpan<SyntaxKind> candidateKinds =
	//	[
	//		SyntaxKind.PrivateKeyword,
	//		SyntaxKind.InternalKeyword,
	//		SyntaxKind.PublicKeyword,
	//	];

	//	using ImmutableArrayBuilder<SyntaxKind> builder = ImmutableArrayBuilder<SyntaxKind>.Rent();
	//	foreach (var syntaxReference in typeSymbol.DeclaringSyntaxReferences)
	//		if (syntaxReference.GetSyntax() is TypeDeclarationSyntax typeDecl)
	//		{
	//			foreach (SyntaxKind kind in candidateKinds)
	//				if (typeDecl.Modifiers.Any(kind))
	//					builder.Add(kind);
	//		}

	//	return builder.ToImmutable();
	//}

	private static SyntaxTokenList GetTypeModifiers(TypeInfo typeInfo)
	{
		SyntaxTokenList propertyModifiers = typeInfo.PropertyAccessibility.ToSyntaxTokenList();
		//foreach (SyntaxKind modifier in typeInfo.GetMethodModifers.AsImmutableArray().FromUnderlyingType())
		//{
		//	propertyModifiers = propertyModifiers.Add(Token(modifier));
		//}
		propertyModifiers = propertyModifiers.Add(Token(SyntaxKind.PartialKeyword));
		return propertyModifiers;
	}

}