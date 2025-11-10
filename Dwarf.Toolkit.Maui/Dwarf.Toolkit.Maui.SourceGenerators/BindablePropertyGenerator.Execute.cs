using Dwarf.Toolkit.Maui.SourceGenerators.Models;
using Dwarf.Toolkit.SourceGenerators.Extensions;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Dwarf.Toolkit.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dwarf.Toolkit.Maui.SourceGenerators;

partial class BindablePropertyGenerator
{
	internal static class Execute
	{
		/// <summary>
		/// Checks whether an input syntax node is a candidate property declaration for the generator.
		/// </summary>
		/// <param name="node">The input syntax node to check.</param>
		/// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
		/// <returns>Whether <paramref name="node"/> is a candidate property declaration.</returns>
		public static bool IsCandidatePropertyDeclaration(SyntaxNode node, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();

			if (node is not PropertyDeclarationSyntax { AccessorList.Accessors: { Count: 2 } accessors, AttributeLists.Count: > 0 } property
				|| !property.Modifiers.Any(SyntaxKind.PartialKeyword)
				|| property.Modifiers.Any(SyntaxKind.StaticKeyword)
				|| accessors[0].Kind() is not (SyntaxKind.GetAccessorDeclaration or SyntaxKind.SetAccessorDeclaration)
				|| accessors[1].Kind() is not (SyntaxKind.GetAccessorDeclaration or SyntaxKind.SetAccessorDeclaration))
			{
				return false;
			}
			return true;
			// The candidate member must be in a type with a base type (as it must derive from ObservableObject)
			//return parentNode?.IsTypeDeclarationWithOrPotentiallyWithBaseTypes<ClassDeclarationSyntax>() == true;
		}

		/// <summary>
		/// Checks whether a given candidate node is valid given a compilation.
		/// At least C# 13 required for partial properties support
		/// </summary>
		/// <param name="semanticModel">The <see cref="SemanticModel"/> instance for the current run.</param>
		/// <returns>Whether <paramref name="node"/> is valid.</returns>
		public static bool IsCandidateValidForCompilation(SemanticModel semanticModel)
			=> semanticModel.Compilation.HasLanguageVersionAtLeastEqualTo(LanguageVersion.CSharp13)
			|| semanticModel.Compilation.IsLanguageVersionPreview();

		/// <summary>
		/// Performs additional checks before running the core generation logic.
		/// </summary>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <returns>Whether <paramref name="memberSymbol"/> is valid.</returns>
		public static bool IsCandidateSymbolValid(ISymbol memberSymbol)
		{
			// Ensure that the property declaration is a partial definition with no implementation
			if (memberSymbol is not IPropertySymbol propertySymbol
				|| propertySymbol is not { IsPartialDefinition: true, PartialImplementationPart: null })
			{
				return false;
			}

			// Also ignore all properties that have an invalid declaration
			// Pointer types are never allowed in either case
			if (propertySymbol.ReturnsByRef || propertySymbol.ReturnsByRefReadonly || propertySymbol.Type.IsRefLikeType
				|| propertySymbol.Type.TypeKind == TypeKind.Pointer || propertySymbol.Type.TypeKind == TypeKind.FunctionPointer)
			{
				return false;
			}

			// We assume all other cases are supported (other failure cases will be detected later)
			return true;
		}

		/// <summary>
		/// Processes a given field or property.
		/// </summary>
		/// <param name="memberSyntax">The <see cref="MemberDeclarationSyntax"/> instance to process.</param>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <param name="semanticModel">The <see cref="SemanticModel"/> instance for the current run.</param>
		/// <param name="options">The options in use for the generator.</param>
		/// <param name="token">The cancellation token for the current operation.</param>
		/// <param name="propertyInfo">The resulting <see cref="PropertyInfo"/> value, if successfully retrieved.</param>
		/// <param name="diagnostics">The resulting diagnostics from the processing operation.</param>
		/// <returns>The resulting <see cref="PropertyInfo"/> instance for <paramref name="memberSymbol"/>, if successful.</returns>
		public static bool TryGetInfo(
			GeneratorAttributeSyntaxContext context,
			CancellationToken token,
			[NotNullWhen(true)] out PropertyInfo? propertyInfo,
			out ImmutableArray<DiagnosticInfo> diagnostics)
		{

			if (context.TargetNode is not PropertyDeclarationSyntax propertySyntax
				|| context.TargetSymbol is not IPropertySymbol propertySymbol
				|| !IsTargetTypeValid(propertySymbol))
			{
				propertyInfo = null;
				diagnostics = [];
				return false;
			}

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<DiagnosticInfo> diagnosticsBuilder = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

			// Get all additional modifiers for the member
			ImmutableArray<SyntaxKind> propertyModifiers = GetPropertyModifiers(propertySyntax);

			token.ThrowIfCancellationRequested();

			var bindableAttrData = context.Attributes.FirstOrDefault(
				attr => attr.AttributeClass?.HasFullyQualifiedMetadataName(BindableAttributeNaming.FullyQualifiedName) == true);
			if (bindableAttrData == null)
			{
				propertyInfo = null;
				diagnostics = [];
				return false;
			}

			propertyInfo = new PropertyInfo(
				propertySyntax.Kind(),
				propertySymbol.Type.GetFullyQualifiedNameWithNullabilityAnnotations(),
				propertySymbol.Name,
				propertyModifiers.AsUnderlyingType(),
				AttributeInfo.Create(bindableAttrData));

			diagnostics = diagnosticsBuilder.ToImmutable();

			return true;
		}

		/// <summary>
		/// Validates the containing type for a given field being annotated.
		/// </summary>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <returns>Whether or not the containing type for <paramref name="memberSymbol"/> is valid.</returns>
		private static bool IsTargetTypeValid(ISymbol memberSymbol)
		{
			return memberSymbol.ContainingType.InheritsFromFullyQualifiedMetadataName("Microsoft.Maui.Controls.BindableObject");
		}

		/// <summary>
		/// Gathers all allowed property modifiers that should be forwarded to the generated property.
		/// </summary>
		/// <param name="memberSyntax">The <see cref="MemberDeclarationSyntax"/> instance to process.</param>
		/// <returns>The returned set of property modifiers, if any.</returns>
		private static ImmutableArray<SyntaxKind> GetPropertyModifiers(MemberDeclarationSyntax memberSyntax)
		{
			// Fields never need to carry additional modifiers along
			if (memberSyntax.IsKind(SyntaxKind.FieldDeclaration))
			{
				return [];
			}

			// We only allow a subset of all possible modifiers (aside from the accessibility modifiers)
			ReadOnlySpan<SyntaxKind> candidateKinds =
			[
				SyntaxKind.NewKeyword,
				SyntaxKind.VirtualKeyword,
				SyntaxKind.SealedKeyword,
				SyntaxKind.OverrideKeyword,
				SyntaxKind.RequiredKeyword
			];

			using ImmutableArrayBuilder<SyntaxKind> builder = ImmutableArrayBuilder<SyntaxKind>.Rent();

			// Track all modifiers from the allowed set on the input property declaration
			foreach (SyntaxKind kind in candidateKinds)
			{
				if (memberSyntax.Modifiers.Any(kind))
				{
					builder.Add(kind);
				}
			}

			return builder.ToImmutable();
		}

		/// <summary>
		/// Gets the <see cref="MemberDeclarationSyntax"/> instance for the input field.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="PropertyInfo"/> instance to process.</param>
		/// <returns>The generated <see cref="MemberDeclarationSyntax"/> instance for <paramref name="propertyInfo"/>.</returns>
		public static ImmutableArray<MemberDeclarationSyntax> GetPropertySyntax(HierarchyInfo hInfo, PropertyInfo propertyInfo)
		{
			// Get the property type syntax
			TypeSyntax propertyType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			// Mark with GeneratedCode attribute
			//
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			AttributeListSyntax[] genCodeAttrMarker = [
						AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
								.AddArgumentListArguments(
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).FullName))),
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).Assembly.GetName().Version.ToString()))))
								)),
						AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage"))))];
			//
			// Prepare for construct static BindableProperty:
			//
			TypeSyntax bipType = IdentifierName("BindableProperty");
			var bipCreateAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, bipType, IdentifierName("Create"));

			using ImmutableArrayBuilder<ArgumentSyntax> bipCreateArgsBuilder = ImmutableArrayBuilder<ArgumentSyntax>.Rent();
			bipCreateArgsBuilder.AddRange([
				Argument(IdentifierName($"nameof({propertyInfo.PropertyName})")),
				Argument(IdentifierName($"typeof({propertyInfo.TypeNameWithNullabilityAnnotations})")),
				Argument(IdentifierName($"typeof({hInfo.MetadataName})"))
			]);

			/*			var bipCreateArgs = ArgumentList([
							Argument(IdentifierName($"nameof({propertyInfo.PropertyName})")),
							Argument(IdentifierName($"typeof({propertyInfo.TypeNameWithNullabilityAnnotations})")),
							Argument(IdentifierName($"typeof({hInfo.MetadataName})"))
							]);
			*/
			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultValueArg, out var defaultArgInfo))
			{
				bipCreateArgsBuilder.Add(Argument(NameColon(IdentifierName("defaultValue")), default, defaultArgInfo.GetSyntax()));
			}

			var bipCreateArgs = ArgumentList(SeparatedList(bipCreateArgsBuilder.AsEnumerable()));

			var bipEqualsClause = EqualsValueClause(InvocationExpression(bipCreateAccess, bipCreateArgs));
			// static BindableProperty defenition:
			//
			// public static readonly BindableProperty <PROPERY_NAME>Property = BindableProperty.Create(nameof(<PROPERY_NAME>), typeof(<PROPERY_TYPE>), typeof(<CLASS_NAME>), ...);
			//
			var staticFiealdDeclaration = FieldDeclaration(
					VariableDeclaration(bipType, SingletonSeparatedList(
						VariableDeclarator(Identifier($"{propertyInfo.PropertyName}Property"), null, bipEqualsClause)
						)))
				.AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword));

			// Construct the generated property as follows:
			//
			// /// <inheritdoc/>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			// partial <PROPERY_TYPE> <PROPERTY_NAME>
			// {
			//		get => (<PROPERY_TYPE>)GetValue(<PROPERY_NAME>Property);
			//		set => SetValue(<PROPERY_NAME>Property, value);
			// }
			var propertyReference = PropertyDeclaration(propertyType, Identifier(propertyInfo.PropertyName))
					.AddAttributeLists(genCodeAttrMarker)
					.WithLeadingTrivia(TriviaList(Comment("/// <inheritdoc/>")))
					.AddModifiers(Token(SyntaxKind.PartialKeyword))
					.AddAccessorListAccessors(
						AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
							.WithExpressionBody(ArrowExpressionClause(
								CastExpression(propertyType,
									InvocationExpression(IdentifierName($"GetValue"),
									ArgumentList(SeparatedList([
										Argument(IdentifierName($"{propertyInfo.PropertyName}Property"))
										]))
								))))
							.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
						AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
							.WithExpressionBody(ArrowExpressionClause(
								InvocationExpression(IdentifierName($"SetValue"),
								ArgumentList(SeparatedList([
									Argument(IdentifierName($"{propertyInfo.PropertyName}Property")),
									Argument(IdentifierName("value"))
									]))
								)))
							.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

			return [staticFiealdDeclaration, propertyReference];
		}
	}
}