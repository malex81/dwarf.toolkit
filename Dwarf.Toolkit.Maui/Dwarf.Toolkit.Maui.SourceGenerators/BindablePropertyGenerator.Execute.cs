using Dwarf.Toolkit.Maui.SourceGenerators.Constants;
using Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics;
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

		static ChangeMethodInfo ExploreMethod(GeneratorAttributeSyntaxContext context, string name)
		{
			static MethodExist CheckPartial(IMethodSymbol? m)
			{
				if (m == null) return MethodExist.No;
				var hasPartial = m.DeclaringSyntaxReferences.Any(rs
					=> rs.GetSyntax() is MethodDeclarationSyntax mSyntax && mSyntax.Modifiers.Any(SyntaxKind.PartialKeyword));
				return hasPartial ? MethodExist.ExistPartial : MethodExist.ExistNoPartial;
			}

			var methods = context.TargetSymbol.ContainingType.GetMembers(name).Where(m => m is IMethodSymbol).Select(m => (IMethodSymbol)m).ToArray();
			var method1 = methods.FirstOrDefault(m => m.Parameters.Length == 1);
			var method2 = methods.FirstOrDefault(m => m.Parameters.Length == 2);

			return new(name, CheckPartial(method1), CheckPartial(method2));
		}

		/// <summary>
		/// Processes a given property.
		/// </summary>
		/// <param name="context">The generator context.</param>
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
			propertyInfo = null;

			if (context.TargetNode is not PropertyDeclarationSyntax propertySyntax
				|| context.TargetSymbol is not IPropertySymbol propertySymbol
				|| !IsTargetTypeValid(propertySymbol))
			{
				diagnostics = [];
				return false;
			}

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<DiagnosticInfo> diagnosticsBuilder = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

			// Get all additional modifiers for the member
			ImmutableArray<SyntaxKind> propertyModifiers = GetPropertyModifiers(propertySyntax);

			token.ThrowIfCancellationRequested();

			var bindableAttrData = context.Attributes.FirstOrDefault(attr => attr.AttributeClass?.HasFullyQualifiedMetadataName(BindableAttributeNaming.FullyQualifiedName) == true);
			if (bindableAttrData == null)
			{
				diagnostics = diagnosticsBuilder.ToImmutable();
				return false;
			}

			if (bindableAttrData.TryGetNamedArgument<object>(BindableAttributeNaming.DefaultValueArg, out _)
				&& bindableAttrData.TryGetNamedArgument<string>(BindableAttributeNaming.DefaultValueExpressionArg, out _))
			{
				diagnosticsBuilder.Add(
					   DiagnosticDescriptors.DefaultValueExprassionWithDefaultValue_Warning,
					   propertySymbol,
					   propertySymbol.Name);
			}

			var bindableAttrInfo = AttributeInfo.Create(bindableAttrData);

			token.ThrowIfCancellationRequested();

			propertyInfo = new PropertyInfo(
				propertySyntax.Kind(),
				propertySymbol.Type.GetFullyQualifiedNameWithNullabilityAnnotations(),
				propertySymbol.Name,
				propertyModifiers.AsUnderlyingType(),
				propertySyntax.Modifiers.ContainsAnyAccessibilityModifiers() ? propertySymbol.DeclaredAccessibility : Accessibility.NotApplicable,
				bindableAttrInfo,
				ExploreMethod(context, bindableAttrInfo.GetNamedTextArgumentValue(BindableAttributeNaming.ChangingMethodArg) ?? $"On{propertySymbol.Name}Changing"),
				ExploreMethod(context, bindableAttrInfo.GetNamedTextArgumentValue(BindableAttributeNaming.ChangedMethodArg) ?? $"On{propertySymbol.Name}Changed"));

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
		/// <param name="hInfo">Contains information about class name</param>
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
			//
			// Add arguments for BindableProperty.Create( ... )
			//
			using ImmutableArrayBuilder<ArgumentSyntax> bipCreateArgsBuilder = ImmutableArrayBuilder<ArgumentSyntax>.Rent();
			bipCreateArgsBuilder.AddRange([
				Argument(IdentifierName($"nameof({propertyInfo.PropertyName})")),
				Argument(IdentifierName($"typeof({propertyInfo.TypeNameWithNullabilityAnnotations})")),
				Argument(IdentifierName($"typeof({hInfo.MetadataName})"))
			]);

			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultValueArg, out var defaultArgInfo))
			{
				bipCreateArgsBuilder.Add(Argument(NameColon(IdentifierName("defaultValue")), default, defaultArgInfo.GetSyntax()));
			}
			else if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultValueExpressionArg, out var defaultExprArgInfo)
				&& defaultExprArgInfo is TypedConstantInfo.Primitive.String defaultCodeInfo)
			{
				bipCreateArgsBuilder.Add(Argument(NameColon(IdentifierName("defaultValue")), default, ParseExpression(defaultCodeInfo.Value)));
			}
			if (propertyInfo.ChangingMethodInfo.Exist1 != MethodExist.No || propertyInfo.ChangingMethodInfo.Exist2 != MethodExist.No)
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("propertyChanging")),
					default,
					ParseExpression(string.Format(ServiceMembers.ChangingMethodFormat, propertyInfo.PropertyName))));
			if (propertyInfo.ChangedMethodInfo.Exist1 != MethodExist.No || propertyInfo.ChangedMethodInfo.Exist2 != MethodExist.No)
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("propertyChanged")),
					default,
					ParseExpression(string.Format(ServiceMembers.ChangedMethodFormat, propertyInfo.PropertyName))));
			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.ValidateMethodArg, out _))
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("validateValue")),
					default,
					ParseExpression(string.Format(ServiceMembers.ValidateMethodFormat, propertyInfo.PropertyName))));
			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.CoerceMethodArg, out _))
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("coerceValue")),
					default,
					ParseExpression(string.Format(ServiceMembers.CoerceMethodFormat, propertyInfo.PropertyName))));

			var bipCreateArgs = ArgumentList(SeparatedList(bipCreateArgsBuilder.AsEnumerable()));
			//
			// Generate right part:
			//  = BindableProperty.Create( [arguments] )
			//
			var bipEqualsClause = EqualsValueClause(InvocationExpression(bipCreateAccess, bipCreateArgs));
			//
			// static BindableProperty defenition:
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
					.WithModifiers(GetPropertyModifiers(propertyInfo))
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
			propertyModifiers = propertyModifiers.Add(Token(SyntaxKind.PartialKeyword));
			return propertyModifiers;
		}

		public static ImmutableArray<MemberDeclarationSyntax> GetOnPropertyChangeMethodsSyntax(HierarchyInfo hInfo, PropertyInfo propertyInfo)
		{
			// Mark with GeneratedCode attribute
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			AttributeListSyntax genCodeAttrMarker = AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
								.AddArgumentListArguments(
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).FullName))),
									AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).Assembly.GetName().Version.ToString()))))
								));
			//

			// Get the property type syntax
			TypeSyntax parameterType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			string onPropertyChangingHandlerName = $"On{propertyInfo.PropertyName}Changing";
			string onPropertyChangedHandlerName = $"On{propertyInfo.PropertyName}Changed";

			var instanceVarDeclaration = LocalDeclarationStatement(
				VariableDeclaration(ParseTypeName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier("_instance"))
					.WithInitializer(EqualsValueClause(
						CastExpression(IdentifierName(hInfo.MetadataName), IdentifierName("bindable"))
					)))));

			MemberDeclarationSyntax staticPropertyChangingDeclaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"__{propertyInfo.PropertyName}_Changing"))
				.AddModifiers(Token(SyntaxKind.StaticKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("bindable")).WithType(IdentifierName("BindableProperty")),
					Parameter(Identifier("oldValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
					Parameter(Identifier("newValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
				.AddAttributeLists(genCodeAttrMarker)
				.WithBody(Block(List<StatementSyntax>([
					instanceVarDeclaration,
					ExpressionStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(onPropertyChangingHandlerName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("newValue"))
						]))),
					ExpressionStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(onPropertyChangingHandlerName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("oldValue")),
							Argument(IdentifierName("newValue"))
						])))
					])));

			MemberDeclarationSyntax staticPropertyChangedDeclaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"__{propertyInfo.PropertyName}_Changed"))
				.AddModifiers(Token(SyntaxKind.StaticKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("bindable")).WithType(IdentifierName("BindableProperty")),
					Parameter(Identifier("oldValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
					Parameter(Identifier("newValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
				.AddAttributeLists(genCodeAttrMarker)
				.WithBody(Block(List<StatementSyntax>([
					instanceVarDeclaration,
					ExpressionStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(onPropertyChangedHandlerName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("newValue"))
						]))),
					ExpressionStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(onPropertyChangedHandlerName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("oldValue")),
							Argument(IdentifierName("newValue"))
						])))
					])));

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> is changing.</summary>
			// /// <param name="value">The new property value being set.</param>
			// /// <remarks>This method is invoked right before the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changing(<PROPERTY_TYPE> value);
			MemberDeclarationSyntax onPropertyChangingDeclaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(onPropertyChangingHandlerName))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
				.AddAttributeLists(genCodeAttrMarker
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> is changing.</summary>"),
						Comment("/// <param name=\"value\">The new property value being set.</param>"),
						Comment($"/// <remarks>This method is invoked right before the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> is changing.</summary>
			// /// <param name="oldValue">The previous property value that is being replaced.</param>
			// /// <param name="newValue">The new property value being set.</param>
			// /// <remarks>This method is invoked right before the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changing(<OLD_VALUE_TYPE> oldValue, <PROPERTY_TYPE> newValue);
			MemberDeclarationSyntax onPropertyChanging2Declaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(onPropertyChangingHandlerName))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("oldValue")).WithType(parameterType),
					Parameter(Identifier("newValue")).WithType(parameterType))
				.AddAttributeLists(genCodeAttrMarker
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> is changing.</summary>"),
						Comment("/// <param name=\"oldValue\">The previous property value that is being replaced.</param>"),
						Comment("/// <param name=\"newValue\">The new property value being set.</param>"),
						Comment($"/// <remarks>This method is invoked right before the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> ust changed.</summary>
			// /// <param name="value">The new property value that was set.</param>
			// /// <remarks>This method is invoked right after the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changed(<PROPERTY_TYPE> value);
			MemberDeclarationSyntax onPropertyChangedDeclaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(onPropertyChangedHandlerName))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
				.AddAttributeLists(genCodeAttrMarker
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> just changed.</summary>"),
						Comment("/// <param name=\"value\">The new property value that was set.</param>"),
						Comment($"/// <remarks>This method is invoked right after the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> ust changed.</summary>
			// /// <param name="oldValue">The previous property value that was replaced.</param>
			// /// <param name="newValue">The new property value that was set.</param>
			// /// <remarks>This method is invoked right after the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changed(<OLD_VALUE_TYPE> oldValue, <PROPERTY_TYPE> newValue);
			MemberDeclarationSyntax onPropertyChanged2Declaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(onPropertyChangedHandlerName))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("oldValue")).WithType(parameterType),
					Parameter(Identifier("newValue")).WithType(parameterType))
				.AddAttributeLists(genCodeAttrMarker
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> just changed.</summary>"),
						Comment("/// <param name=\"oldValue\">The previous property value that was replaced.</param>"),
						Comment("/// <param name=\"newValue\">The new property value that was set.</param>"),
						Comment($"/// <remarks>This method is invoked right after the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			return [staticPropertyChangingDeclaration, staticPropertyChangedDeclaration, onPropertyChangingDeclaration, onPropertyChanging2Declaration, onPropertyChangedDeclaration, onPropertyChanged2Declaration];
		}
	}
}