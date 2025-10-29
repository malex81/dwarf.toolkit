using Dwarf.Toolkit.Maui.SourceGenerators.Extensions;
using Dwarf.Toolkit.Maui.SourceGenerators.Helpers;
using Dwarf.Toolkit.Maui.SourceGenerators.Models;
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
		/// Gets the candidate <see cref="MemberDeclarationSyntax"/> after the initial filtering.
		/// </summary>
		/// <param name="node">The input syntax node to convert.</param>
		/// <returns>The resulting <see cref="MemberDeclarationSyntax"/> instance.</returns>
		public static MemberDeclarationSyntax GetCandidateMemberDeclaration(SyntaxNode node)
		{
			// If the node is a property declaration, just return it directly. Note that we don't have
			// to check whether we're using Roslyn 4.12 here, as if that's not the case all of these
			// syntax nodes would already have pre-filtered well before this method could run at all.
			if (node is PropertyDeclarationSyntax propertySyntax)
			{
				return propertySyntax;
			}

			// Otherwise, assume all targets are field declarations
			return (MemberDeclarationSyntax)node.Parent!.Parent!;
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
			MemberDeclarationSyntax memberSyntax,
			ISymbol memberSymbol,
			SemanticModel semanticModel,
			CancellationToken token,
			[NotNullWhen(true)] out PropertyInfo? propertyInfo,
			out ImmutableArray<DiagnosticInfo> diagnostics)
		{
			// Validate the target type
			if (!IsTargetTypeValid(memberSymbol))
			{
				propertyInfo = null;
				diagnostics = [];
				return false;
			}

			token.ThrowIfCancellationRequested();

			// Get the property type and name
			string typeNameWithNullabilityAnnotations = GetPropertyType(memberSymbol).GetFullyQualifiedNameWithNullabilityAnnotations();
			if (memberSymbol is not IPropertySymbol propertySymbol)
			{
				propertyInfo = null;
				diagnostics = [];
				return false;
			}

			string propertyName = propertySymbol.Name;

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<string> propertyChangedNames = ImmutableArrayBuilder<string>.Rent();

			// The current property is always notified
			propertyChangedNames.Add(propertyName);

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<DiagnosticInfo> builder = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

			// Get all additional modifiers for the member
			ImmutableArray<SyntaxKind> propertyModifiers = GetPropertyModifiers(memberSyntax);

			// Retrieve the accessibility values for all components
			if (!TryGetAccessibilityModifiers(
				memberSyntax,
				memberSymbol,
				out Accessibility propertyAccessibility,
				out Accessibility getterAccessibility,
				out Accessibility setterAccessibility))
			{
				propertyInfo = null;
				diagnostics = builder.ToImmutable();

				return false;
			}

			token.ThrowIfCancellationRequested();

			propertyInfo = new PropertyInfo(
				memberSyntax.Kind(),
				typeNameWithNullabilityAnnotations,
				propertyName,
				propertyModifiers.AsUnderlyingType(),
				propertyAccessibility,
				getterAccessibility,
				setterAccessibility);

			diagnostics = builder.ToImmutable();

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
		/// Tries to get the accessibility of the property and accessors, if possible.
		/// If the target member is not a property, it will use the defaults.
		/// </summary>
		/// <param name="memberSyntax">The <see cref="MemberDeclarationSyntax"/> instance to process.</param>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <param name="propertyAccessibility">The accessibility of the property, if available.</param>
		/// <param name="getterAccessibility">The accessibility of the <see langword="get"/> accessor, if available.</param>
		/// <param name="setterAccessibility">The accessibility of the <see langword="set"/> accessor, if available.</param>
		/// <returns>Whether the property was valid and the accessibilities could be retrieved.</returns>
		private static bool TryGetAccessibilityModifiers(
			MemberDeclarationSyntax memberSyntax,
			ISymbol memberSymbol,
			out Accessibility propertyAccessibility,
			out Accessibility getterAccessibility,
			out Accessibility setterAccessibility)
		{
			// For legacy support for fields, the property that is generated is public, and neither
			// accessors will have any accessibility modifiers. To customize the accessibility,
			// partial properties should be used instead.
			if (memberSyntax.IsKind(SyntaxKind.FieldDeclaration))
			{
				propertyAccessibility = Accessibility.Public;
				getterAccessibility = Accessibility.NotApplicable;
				setterAccessibility = Accessibility.NotApplicable;

				return true;
			}

			propertyAccessibility = Accessibility.NotApplicable;
			getterAccessibility = Accessibility.NotApplicable;
			setterAccessibility = Accessibility.NotApplicable;

			// Ensure that we have a getter and a setter, and that the setter is not init-only
			if (memberSymbol is not IPropertySymbol { GetMethod: { } getMethod, SetMethod: { IsInitOnly: false } setMethod })
			{
				return false;
			}

			// At this point the node is definitely a property, just do a sanity check
			if (memberSyntax is not PropertyDeclarationSyntax propertySyntax)
			{
				return false;
			}

			// Track the property accessibility if explicitly set
			if (propertySyntax.Modifiers.ContainsAnyAccessibilityModifiers())
			{
				propertyAccessibility = memberSymbol.DeclaredAccessibility;
			}

			// Track the accessors accessibility, if explicitly set
			foreach (AccessorDeclarationSyntax accessor in propertySyntax.AccessorList?.Accessors ?? [])
			{
				if (!accessor.Modifiers.ContainsAnyAccessibilityModifiers())
				{
					continue;
				}

				switch (accessor.Kind())
				{
					case SyntaxKind.GetAccessorDeclaration:
						getterAccessibility = getMethod.DeclaredAccessibility;
						break;
					case SyntaxKind.SetAccessorDeclaration:
						setterAccessibility = setMethod.DeclaredAccessibility;
						break;
				}
			}

			return true;
		}

		/// <summary>
		/// Gets the <see cref="MemberDeclarationSyntax"/> instance for the input field.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="PropertyInfo"/> instance to process.</param>
		/// <returns>The generated <see cref="MemberDeclarationSyntax"/> instance for <paramref name="propertyInfo"/>.</returns>
		public static ImmutableArray<MemberDeclarationSyntax> GetPropertySyntax(PropertyInfo propertyInfo)
		{
			using ImmutableArrayBuilder<StatementSyntax> setterStatements = ImmutableArrayBuilder<StatementSyntax>.Rent();

			ExpressionSyntax getterFieldExpression;
			ExpressionSyntax setterFieldExpression;

			getterFieldExpression = setterFieldExpression = IdentifierName("_temp");

			// Add the OnPropertyChanging() call first:
			//
			// On<PROPERTY_NAME>Changing(value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changing"))
					.AddArgumentListArguments(Argument(IdentifierName("value")))));


			// Add the OnPropertyChanged() call:
			//
			// On<PROPERTY_NAME>Changed(value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changed"))
					.AddArgumentListArguments(Argument(IdentifierName("value")))));


			// Get the property type syntax
			TypeSyntax propertyType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			// Generate the inner setter block as follows:
			//
			// if (!global::System.Collections.Generic.EqualityComparer<<PROPERTY_TYPE>>.Default.Equals(<FIELD_EXPRESSION>, value))
			// {
			//     <STATEMENTS>
			// }
			IfStatementSyntax setterIfStatement =
				IfStatement(
					PrefixUnaryExpression(
						SyntaxKind.LogicalNotExpression,
						InvocationExpression(
							MemberAccessExpression(
								SyntaxKind.SimpleMemberAccessExpression,
								MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									GenericName(Identifier("global::System.Collections.Generic.EqualityComparer"))
									.AddTypeArgumentListArguments(propertyType),
									IdentifierName("Default")),
								IdentifierName("Equals")))
						.AddArgumentListArguments(
							Argument(setterFieldExpression),
							Argument(IdentifierName("value")))),
					Block(setterStatements.AsEnumerable()));

			// Prepare the setter for the generated property:
			//
			// <SETTER_ACCESSIBILITY> set
			// {
			//     <BODY>
			// }
			AccessorDeclarationSyntax setAccessor = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
				.WithModifiers(propertyInfo.SetterAccessibility.ToSyntaxTokenList())
				.WithBody(Block(setterIfStatement));

			AttributeListSyntax[] genCodeAttrMarker = [
						AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
								.AddArgumentListArguments(
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).FullName))),
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).Assembly.GetName().Version.ToString()))))
								)),
						AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage"))))];


			// Construct static BindableProperty defenition:
			//
			// public static readonly BindableProperty <PROPERY_NAME>Property = BindableProperty.Create(nameof(<PROPERY_NAME>), typeof(<PROPERY_TYPE>), typeof(<CLASS_NAME>), ...);
			TypeSyntax declarationFiealdType = IdentifierName("BindableProperty");
			var staticFiealdDeclaration = FieldDeclaration(
					VariableDeclaration(declarationFiealdType)
						.AddVariables(VariableDeclarator($"{propertyInfo.PropertyName}Property")))
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

		/// <summary>
		/// Gets all modifiers that need to be added to a generated property.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="PropertyInfo"/> instance to process.</param>
		/// <returns>The list of necessary modifiers for <paramref name="propertyInfo"/>.</returns>
		private static SyntaxTokenList GetPropertyModifiers(PropertyInfo propertyInfo)
		{
			SyntaxTokenList propertyModifiers = propertyInfo.PropertyAccessibility.ToSyntaxTokenList();

			// Add all gathered modifiers
			foreach (SyntaxKind modifier in propertyInfo.PropertyModifers.AsImmutableArray().FromUnderlyingType())
			{
				propertyModifiers = propertyModifiers.Add(Token(modifier));
			}

			// Add the 'partial' modifier if the original member is a partial property
			if (propertyInfo.AnnotatedMemberKind is SyntaxKind.PropertyDeclaration)
			{
				propertyModifiers = propertyModifiers.Add(Token(SyntaxKind.PartialKeyword));
			}

			return propertyModifiers;
		}

		/// <summary>
		/// Gets the <see cref="ITypeSymbol"/> for a given member symbol (it can be either a field or a property).
		/// </summary>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <returns>The type of <paramref name="memberSymbol"/>.</returns>
		public static ITypeSymbol GetPropertyType(ISymbol memberSymbol)
		{
			// Check if the member is a property first
			if (memberSymbol is IPropertySymbol propertySymbol)
			{
				return propertySymbol.Type;
			}

			// Otherwise, the only possible case is a field symbol
			return ((IFieldSymbol)memberSymbol).Type;
		}
	}
}