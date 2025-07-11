﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;

/// <summary>
/// A model describing a type info in a type hierarchy.
/// </summary>
/// <param name="QualifiedName">The qualified name for the type.</param>
/// <param name="Kind">The type of the type in the hierarchy.</param>
/// <param name="IsRecord">Whether the type is a record type.</param>
internal sealed record TypeInfo(string QualifiedName, TypeKind Kind, bool IsRecord)
{
	/// <summary>
	/// Creates a <see cref="TypeDeclarationSyntax"/> instance for the current info.
	/// </summary>
	/// <returns>A <see cref="TypeDeclarationSyntax"/> instance for the current info.</returns>
	public TypeDeclarationSyntax GetSyntax()
	{
		// Create the partial type declaration with the kind.
		// This code produces a class declaration as follows:
		//
		// <TYPE_KIND> <TYPE_NAME>
		// {
		// }
		//
		// Note that specifically for record declarations, we also need to explicitly add the open
		// and close brace tokens, otherwise member declarations will not be formatted correctly.
		return Kind switch
		{
			TypeKind.Struct => StructDeclaration(QualifiedName),
			TypeKind.Interface => InterfaceDeclaration(QualifiedName),
			TypeKind.Class when IsRecord =>
				RecordDeclaration(Token(SyntaxKind.RecordKeyword), QualifiedName)
				.WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
				.WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken)),
			_ => ClassDeclaration(QualifiedName)
		};
	}
}