﻿using Dwarf.Toolkit.Maui.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Immutable;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;

/// <summary>
/// A model for a serializeable diagnostic info.
/// </summary>
/// <param name="Descriptor">The wrapped <see cref="DiagnosticDescriptor"/> instance.</param>
/// <param name="SyntaxTree">The tree to use as location for the diagnostic, if available.</param>
/// <param name="TextSpan">The span to use as location for the diagnostic.</param>
/// <param name="Arguments">The diagnostic arguments.</param>
internal sealed record DiagnosticInfo(
	DiagnosticDescriptor Descriptor,
	SyntaxTree? SyntaxTree,
	TextSpan TextSpan,
	EquatableArray<string> Arguments)
{
	/// <summary>
	/// Creates a new <see cref="Diagnostic"/> instance with the state from this model.
	/// </summary>
	/// <returns>A new <see cref="Diagnostic"/> instance with the state from this model.</returns>
	public Diagnostic ToDiagnostic()
	{
		if (SyntaxTree is not null)
		{
			return Diagnostic.Create(Descriptor, Location.Create(SyntaxTree, TextSpan), Arguments.ToArray());
		}

		return Diagnostic.Create(Descriptor, null, Arguments.ToArray());
	}

	/// <summary>
	/// Creates a new <see cref="DiagnosticInfo"/> instance with the specified parameters.
	/// </summary>
	/// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
	/// <param name="symbol">The source <see cref="ISymbol"/> to attach the diagnostics to.</param>
	/// <param name="args">The optional arguments for the formatted message to include.</param>
	/// <returns>A new <see cref="DiagnosticInfo"/> instance with the specified parameters.</returns>
	public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
	{
		Location location = symbol.Locations.First();

		return new(descriptor, location.SourceTree, location.SourceSpan, args.Select(static arg => arg.ToString()).ToImmutableArray());
	}

	/// <summary>
	/// Creates a new <see cref="DiagnosticInfo"/> instance with the specified parameters.
	/// </summary>
	/// <param name="descriptor">The input <see cref="DiagnosticDescriptor"/> for the diagnostics to create.</param>
	/// <param name="node">The source <see cref="SyntaxNode"/> to attach the diagnostics to.</param>
	/// <param name="args">The optional arguments for the formatted message to include.</param>
	/// <returns>A new <see cref="DiagnosticInfo"/> instance with the specified parameters.</returns>
	public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, SyntaxNode node, params object[] args)
	{
		Location location = node.GetLocation();

		return new(descriptor, location.SourceTree, location.SourceSpan, args.Select(static arg => arg.ToString()).ToImmutableArray());
	}
}