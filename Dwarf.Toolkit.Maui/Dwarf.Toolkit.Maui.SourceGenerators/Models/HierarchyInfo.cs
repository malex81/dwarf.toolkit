﻿using Dwarf.Toolkit.Maui.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;

/// <summary>
/// A model describing the hierarchy info for a specific type.
/// </summary>
/// <param name="FilenameHint">The filename hint for the current type.</param>
/// <param name="MetadataName">The metadata name for the current type.</param>
/// <param name="Namespace">Gets the namespace for the current type.</param>
/// <param name="Hierarchy">Gets the sequence of type definitions containing the current type.</param>
internal sealed partial record HierarchyInfo(string FilenameHint, string MetadataName, string Namespace, EquatableArray<TypeInfo> Hierarchy)
{
	/// <summary>
	/// Creates a new <see cref="HierarchyInfo"/> instance from a given <see cref="INamedTypeSymbol"/>.
	/// </summary>
	/// <param name="typeSymbol">The input <see cref="INamedTypeSymbol"/> instance to gather info for.</param>
	/// <returns>A <see cref="HierarchyInfo"/> instance describing <paramref name="typeSymbol"/>.</returns>
	public static HierarchyInfo From(INamedTypeSymbol typeSymbol)
	{
		using ImmutableArrayBuilder<TypeInfo> hierarchy = ImmutableArrayBuilder<TypeInfo>.Rent();

		for (INamedTypeSymbol? parent = typeSymbol;
			 parent is not null;
			 parent = parent.ContainingType)
		{
			hierarchy.Add(new TypeInfo(
				parent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
				parent.TypeKind,
				parent.IsRecord));
		}

		return new(
			typeSymbol.GetFullyQualifiedMetadataName(),
			typeSymbol.MetadataName,
			typeSymbol.ContainingNamespace.ToDisplayString(new(typeQualificationStyle: NameAndContainingTypesAndNamespaces)),
			hierarchy.ToImmutable());
	}
}