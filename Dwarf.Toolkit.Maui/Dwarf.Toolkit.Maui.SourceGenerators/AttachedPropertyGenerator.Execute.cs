using Dwarf.Toolkit.Maui.SourceGenerators.Constants;
using Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics;
using Dwarf.Toolkit.Maui.SourceGenerators.Models;
using Dwarf.Toolkit.SourceGenerators.Extensions;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Dwarf.Toolkit.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dwarf.Toolkit.Maui.SourceGenerators;

partial class AttachedPropertyGenerator
{
	internal static class Execute
	{
		/// <summary>
		/// Checks whether an input syntax node is a candidate property declaration for the generator.
		/// </summary>
		/// <param name="node">The input syntax node to check.</param>
		/// <param name="token">The <see cref="CancellationToken"/> used to cancel the operation, if needed.</param>
		/// <returns>Whether <paramref name="node"/> is a candidate property declaration.</returns>
		public static bool IsCandidateMethodDeclaration(SyntaxNode node, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();

			if (node is not MethodDeclarationSyntax method
				|| !method.Modifiers.Any(SyntaxKind.PartialKeyword)
				|| !method.Modifiers.Any(SyntaxKind.StaticKeyword))
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
			if (memberSymbol is not IMethodSymbol methodSymbol
				|| methodSymbol is not { IsPartialDefinition: true, PartialImplementationPart: null })
			{
				return false;
			}

			// Also ignore all properties that have an invalid declaration
			// Pointer types are never allowed in either case
			if (methodSymbol.ReturnsVoid || methodSymbol.ReturnsByRef || methodSymbol.ReturnsByRefReadonly || methodSymbol.ReturnType.IsRefLikeType
				|| methodSymbol.ReturnType.TypeKind == TypeKind.Pointer || methodSymbol.ReturnType.TypeKind == TypeKind.FunctionPointer)
			{
				return false;
			}

			// We assume all other cases are supported (other failure cases will be detected later)
			return true;
		}
		static bool IsBindableObjectFirstParameter(IMethodSymbol method)
			=> method.Parameters.Any() && method.Parameters[0].Type.HasOrInheritsFromFullyQualifiedMetadataName(CommonTypes.BindableObject);
		/// <summary>
		/// Used for Changing and Changed methods
		/// </summary>
		/// <param name="context"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		static ChangeMethodInfo ExploreMethod(GeneratorAttributeSyntaxContext context, string name)
		{
			static MethodExist CheckPartial(IMethodSymbol? m)
			{
				if (m == null) return MethodExist.No;
				var hasPartial = m.HasPartialModifier();
				return hasPartial ? MethodExist.ExistPartial : MethodExist.ExistNoPartial;
			}

			var methods = context.TargetSymbol.ContainingType
				.GetMembers(name)
				.Where(m => m.IsStatic && m is IMethodSymbol mth && mth.Parameters.Length is (2 or 3) && IsBindableObjectFirstParameter(mth))
				.Select(m => (IMethodSymbol)m).ToArray();
			var method1 = methods.FirstOrDefault(m => m.Parameters.Length == 2);
			var method2 = methods.FirstOrDefault(m => m.Parameters.Length == 3);

			return new(name, CheckPartial(method1), CheckPartial(method2));
		}
		/// <summary>
		/// Used for validation and coerce methods
		/// </summary>
		/// <param name="context"></param>
		/// <param name="name"></param>
		/// <param name="returnType"></param>
		/// <param name="prmTypes"></param>
		/// <returns></returns>
		static bool FindNoPartialMethod(GeneratorAttributeSyntaxContext context, string name, string returnType, params string[] prmTypes)
			=> context.TargetSymbol.ContainingType.GetMembers(name)
				.Any(s =>
				{
					if (s is not IMethodSymbol m || !m.IsStatic || m.HasPartialModifier())
						return false;

					if (!m.ReturnType.HasFullyQualifiedName(returnType))
						return false;

					if (!IsBindableObjectFirstParameter(m) || m.Parameters.Length != prmTypes.Length + 1)
						return false;

					for (int i = 0; i < prmTypes.Length; i++)
						if (!m.Parameters[i + 1].Type.HasFullyQualifiedName(prmTypes[i]))
							return false;

					return true;
				});
		/// <summary>
		/// Parse the method to which the attribute was applied
		/// </summary>
		/// <param name="method"></param>
		/// <param name="diagnostics"></param>
		/// <param name="propertyName"></param>
		/// <param name="targetType"></param>
		/// <returns></returns>
		static bool TryParseSourceMethod(IMethodSymbol method,
			ImmutableArrayBuilder<DiagnosticInfo> diagnostics,
			[MaybeNullWhen(false)] out string propertyName,
			[MaybeNullWhen(false)] out string targetType)
		{
			if (method.Name.Length < 4 || !method.Name.StartsWith("Get"))
			{
				propertyName = null;
				targetType = null;
				diagnostics.Add(
					   DiagnosticDescriptors.InvalidGetMethodForAttachedProperty_Error,
					   method,
					   method.Name,
					   "name");
				return false;
			}
			propertyName = method.Name[3..];
			if (method.Parameters.Length != 1)
			{
				targetType = null;
				diagnostics.Add(
					   DiagnosticDescriptors.InvalidGetMethodForAttachedProperty_Error,
					   method,
					   method.Name,
					   "set of parameters");
				return false;
			}
			targetType = method.Parameters[0].Type.GetFullyQualifiedName();
			if (!IsBindableObjectFirstParameter(method))
			{
				diagnostics.Add(
					   DiagnosticDescriptors.InvalidGetMethodForAttachedProperty_Error,
					   method,
					   method.Name,
					   "parameter type");
				return false;
			}
			return true;
		}
		/// <summary>
		/// Processes a given property.
		/// </summary>
		/// <param name="context">The generator context.</param>
		/// <param name="token">The cancellation token for the current operation.</param>
		/// <param name="propertyInfo">The resulting <see cref="AttachedPropertyInfo"/> value, if successfully retrieved.</param>
		/// <param name="diagnostics">The resulting diagnostics from the processing operation.</param>
		/// <returns>The resulting <see cref="AttachedPropertyInfo"/> instance for <paramref name="memberSymbol"/>, if successful.</returns>
		public static bool TryGetInfo(
			GeneratorAttributeSyntaxContext context,
			CancellationToken token,
			[NotNullWhen(true)] out AttachedPropertyInfo? propertyInfo,
			out ImmutableArray<DiagnosticInfo> diagnostics)
		{
			propertyInfo = null;

			if (context.TargetNode is not MethodDeclarationSyntax methodSyntax
				|| context.TargetSymbol is not IMethodSymbol methodSymbol)
			{
				diagnostics = [];
				return false;
			}

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<DiagnosticInfo> diagnosticsBuilder = ImmutableArrayBuilder<DiagnosticInfo>.Rent();

			// Get all additional modifiers for the member
			ImmutableArray<SyntaxKind> methodModifiers = GetMethodModifiers(methodSyntax);

			token.ThrowIfCancellationRequested();

			var bindableAttrData = context.Attributes.FirstOrDefault(attr => attr.AttributeClass?.HasFullyQualifiedMetadataName(BindableAttributeNaming.AttachedFullyQualifiedName) == true);
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
					   methodSymbol,
					   methodSymbol.Name);
			}

			var bindableAttrInfo = AttributeInfo.Create(bindableAttrData);

			token.ThrowIfCancellationRequested();

			if (!TryParseSourceMethod(methodSymbol, diagnosticsBuilder, out var propertyName, out var targetTypeName))
			{
				diagnostics = diagnosticsBuilder.ToImmutable();
				return false;
			}

			string fullyPropertyTypeName = methodSymbol.ReturnType.GetFullyQualifiedNameWithNullabilityAnnotations();
			string realPropertyTypeName = methodSymbol.ReturnType.GetFullyQualifiedName();

			var needGeneratePartialValidation
				= bindableAttrData.TryGetNamedArgument<string>(BindableAttributeNaming.ValidateMethodArg, out var validateMethodName)
					&& validateMethodName != null
					&& !FindNoPartialMethod(context, validateMethodName, "bool", fullyPropertyTypeName);

			token.ThrowIfCancellationRequested();

			var needGeneratePartialCoerce
				= bindableAttrData.TryGetNamedArgument<string>(BindableAttributeNaming.CoerceMethodArg, out var coerceMethodName)
					&& coerceMethodName != null
					&& !FindNoPartialMethod(context, coerceMethodName, fullyPropertyTypeName, fullyPropertyTypeName);

			propertyInfo = new AttachedPropertyInfo(
				fullyPropertyTypeName,
				realPropertyTypeName,
				targetTypeName,
				propertyName,
				methodModifiers.AsUnderlyingType(),
				methodSyntax.Modifiers.ContainsAnyAccessibilityModifiers() ? methodSymbol.DeclaredAccessibility : Accessibility.NotApplicable,
				bindableAttrInfo,
				ExploreMethod(context, bindableAttrInfo.GetNamedTextArgumentValue(BindableAttributeNaming.ChangingMethodArg) ?? $"On{propertyName}Changing"),
				ExploreMethod(context, bindableAttrInfo.GetNamedTextArgumentValue(BindableAttributeNaming.ChangedMethodArg) ?? $"On{propertyName}Changed"),
				needGeneratePartialValidation,
				needGeneratePartialCoerce);

			diagnostics = diagnosticsBuilder.ToImmutable();

			return true;
		}

		/// <summary>
		/// Gathers all allowed property modifiers that should be forwarded to the generated property.
		/// </summary>
		/// <param name="memberSyntax">The <see cref="MemberDeclarationSyntax"/> instance to process.</param>
		/// <returns>The returned set of property modifiers, if any.</returns>
		private static ImmutableArray<SyntaxKind> GetMethodModifiers(MemberDeclarationSyntax memberSyntax)
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
		/// Gets all modifiers that need to be added to a generated methods.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="AttachedPropertyInfo"/> instance to process.</param>
		/// <returns>The list of necessary modifiers for <paramref name="propertyInfo"/>.</returns>
		private static SyntaxTokenList GetMethodModifiers(AttachedPropertyInfo propertyInfo, bool withPartial)
		{
			SyntaxTokenList propertyModifiers = propertyInfo.GetMethodAccessibility.ToSyntaxTokenList();
			// Add all gathered modifiers
			foreach (SyntaxKind modifier in propertyInfo.GetMethodModifers.AsImmutableArray().FromUnderlyingType())
			{
				propertyModifiers = propertyModifiers.Add(Token(modifier));
			}
			if (withPartial)
				propertyModifiers = propertyModifiers.Add(Token(SyntaxKind.PartialKeyword));
			return propertyModifiers;
		}

		/// <summary>
		/// Gets the <see cref="MemberDeclarationSyntax"/> instance for the input field.
		/// </summary>
		/// <param name="hInfo">Contains information about class name</param>
		/// <param name="propertyInfo">The input <see cref="AttachedPropertyInfo"/> instance to process.</param>
		/// <returns>The generated <see cref="MemberDeclarationSyntax"/> instance for <paramref name="propertyInfo"/>.</returns>
		public static ImmutableArray<MemberDeclarationSyntax> GetPropertySyntax(HierarchyInfo hInfo, AttachedPropertyInfo propertyInfo)
		{
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
			TypeSyntax bipType = IdentifierName(CommonTypes.BindableProperty);
			var bipCreateAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, bipType, IdentifierName("CreateAttached"));
			//
			// Add arguments for BindableProperty.Create( ... )
			//
			using ImmutableArrayBuilder<ArgumentSyntax> bipCreateArgsBuilder = ImmutableArrayBuilder<ArgumentSyntax>.Rent();

			var propDefaultExpressionSyntax
				= propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultValueArg, out var defaultArgInfo) ? defaultArgInfo.GetSyntax()
				: propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultValueExpressionArg, out var defaultExprArgInfo)
					&& defaultExprArgInfo is TypedConstantInfo.Primitive.String defaultCodeInfo ? ParseExpression(defaultCodeInfo.Value)
				: IdentifierName("default");

			bipCreateArgsBuilder.AddRange([
				Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(propertyInfo.PropertyName))),
				Argument(IdentifierName($"typeof({propertyInfo.RealTypeName})")),
				Argument(IdentifierName($"typeof({hInfo.MetadataName})")),
				Argument(propDefaultExpressionSyntax)
			]);

			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.DefaultBindingModeArg, out var defBindModeInfo)
				&& defBindModeInfo is TypedConstantInfo.Enum bindingModeEnum)
			{
				var bindingModeMauiEnum = new TypedConstantInfo.Enum("global::Microsoft.Maui.Controls.BindingMode", bindingModeEnum.Value);
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("defaultBindingMode")),
					default,
					bindingModeMauiEnum.GetSyntax()));
			}
			if (propertyInfo.ChangingMethodInfo.Exist1 != MethodExist.No || propertyInfo.ChangingMethodInfo.Exist2 != MethodExist.No)
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("propertyChanging")),
					default,
					ParseExpression(propertyInfo.Srv_PropertyChanging)));
			if (propertyInfo.ChangedMethodInfo.Exist1 != MethodExist.No || propertyInfo.ChangedMethodInfo.Exist2 != MethodExist.No)
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("propertyChanged")),
					default,
					ParseExpression(propertyInfo.Srv_PropertyChanged)));
			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.ValidateMethodArg, out _))
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("validateValue")),
					default,
					ParseExpression(propertyInfo.Srv_ValidateValue)));
			if (propertyInfo.BindableAttribute.TryGetNamedArgumentInfo(BindableAttributeNaming.CoerceMethodArg, out _))
				bipCreateArgsBuilder.Add(Argument(NameColon(
					IdentifierName("coerceValue")),
					default,
					ParseExpression(propertyInfo.Srv_CoerceValue)));

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
				.AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword))
				.WithLeadingTrivia(TriviaList(
						Comment("/// <summary>"),
						Comment($"/// Creates an attached property named {propertyInfo.PropertyName}, of type <see cref=\"{propertyInfo.RealTypeName}\"/>"),
						Comment("/// </summary>")));

			// Construct methods Get<PROPERTY_NAME>:
			//
			// /// <inheritdoc/>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			// public partial <PROPERY_TYPE> Get<PROPERTY_NAME>(BindableObject target) => (<PROPERY_TYPE>)target.GetValue(<PROPERY_NAME>Property);
			var getMethodDeclaration = MethodDeclaration(IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations), $"Get{propertyInfo.PropertyName}")
					.AddAttributeLists(genCodeAttrMarker)
					.WithLeadingTrivia(TriviaList(Comment("/// <inheritdoc/>")))
					.WithModifiers(GetMethodModifiers(propertyInfo, true))
					.AddParameterListParameters(Parameter(Identifier("target")).WithType(IdentifierName(propertyInfo.TargetTypeName)))
					.WithExpressionBody(ArrowExpressionClause(
						InvocationExpression(
							MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
									IdentifierName("target"),
									IdentifierName("GetValue")),
							ArgumentList(SeparatedList([
									Argument(IdentifierName($"{propertyInfo.PropertyName}Property"))
								]))).CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object)))
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			// Construct methods Set<PROPERTY_NAME>:
			//
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			// public static void Set<PROPERTY_NAME>(BindableObject target, <PROPERY_TYPE> value) => target.SetValue(<PROPERY_NAME>Property, value);
			var setMethodDeclaration = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), $"Set{propertyInfo.PropertyName}")
					.AddAttributeLists(genCodeAttrMarker)
					.WithModifiers(GetMethodModifiers(propertyInfo, false))
					.AddParameterListParameters(
						Parameter(Identifier("target")).WithType(IdentifierName(propertyInfo.TargetTypeName)),
						Parameter(Identifier("value")).WithType(IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations)))
					.WithExpressionBody(ArrowExpressionClause(
						InvocationExpression(
							MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
									IdentifierName("target"),
									IdentifierName("SetValue")),
							ArgumentList(SeparatedList([
									Argument(IdentifierName($"{propertyInfo.PropertyName}Property")),
									Argument(IdentifierName("value"))
								]))
							)))
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			return [staticFiealdDeclaration, getMethodDeclaration, setMethodDeclaration];
		}

		// Attribute markers for service generated code
		// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
		readonly static AttributeListSyntax GeneratedCodeAttrMarker = AttributeList(SingletonSeparatedList(
				Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
					.AddArgumentListArguments(
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).FullName))),
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).Assembly.GetName().Version.ToString()))))
					));
		// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
		readonly static AttributeListSyntax ExcludeFromCodeCoverageAttrMarker = AttributeList(SingletonSeparatedList(
				Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage"))));
		// [global::System.Diagnostics.DebuggerNonUserCode]
		readonly static AttributeListSyntax NonUserCodeAttrMarker = AttributeList(SingletonSeparatedList(
				Attribute(IdentifierName("global::System.Diagnostics.DebuggerNonUserCode"))));
		// [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
		readonly static AttributeListSyntax NeverBrowsableAttrMarker = AttributeList(SingletonSeparatedList(
				Attribute(IdentifierName("global::System.ComponentModel.EditorBrowsable"))
					.AddArgumentListArguments(
						AttributeArgument(ParseExpression("global::System.ComponentModel.EditorBrowsableState.Never")))
					));

		/// <summary>
		/// Generate code for On<PropertyName>Changing and On<PropertyName>Changed handlers
		/// </summary>
		/// <param name="hInfo"></param>
		/// <param name="propertyInfo"></param>
		/// <param name="serviceMethodName"></param>
		/// <param name="methodInfo"></param>
		/// <returns></returns>
		static IEnumerable<MemberDeclarationSyntax> GenerateChangeHandlers(
			AttachedPropertyInfo propertyInfo,
			string serviceMethodName,
			ChangeMethodInfo methodInfo)
		{
			// Get the property type syntax
			TypeSyntax parameterType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			if (methodInfo.Exist1 != MethodExist.ExistNoPartial)
			{
				// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
				// partial void On<PROPERTY_NAME><Changing/Changed>(BindableObject target, <PROPERTY_TYPE> value);
				yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(methodInfo.Name))
					.AddModifiers(Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.PartialKeyword))
					.AddParameterListParameters(
						Parameter(Identifier("target")).WithType(IdentifierName(propertyInfo.TargetTypeName)),
						Parameter(Identifier("value")).WithType(parameterType))
					.AddAttributeLists(GeneratedCodeAttrMarker)
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
			}

			if (methodInfo.Exist2 != MethodExist.ExistNoPartial)
			{
				// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
				// partial void On<PROPERTY_NAME><Changing/Changed>(BindableObject target, <PROPERTY_TYPE> oldValue, <PROPERTY_TYPE> newValue);
				yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(methodInfo.Name))
					.AddModifiers(Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.PartialKeyword))
					.AddParameterListParameters(
						Parameter(Identifier("target")).WithType(IdentifierName(propertyInfo.TargetTypeName)),
						Parameter(Identifier("oldValue")).WithType(parameterType),
						Parameter(Identifier("newValue")).WithType(parameterType))
					.AddAttributeLists(GeneratedCodeAttrMarker)
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
			}

			if (methodInfo.Exist1 != MethodExist.No || methodInfo.Exist2 != MethodExist.No)
			{
				yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(serviceMethodName))
					.AddModifiers(Token(SyntaxKind.StaticKeyword))
					.AddParameterListParameters(
						Parameter(Identifier("bindable")).WithType(IdentifierName("BindableObject")),
						Parameter(Identifier("oldValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
						Parameter(Identifier("newValue")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
					.AddAttributeLists(GeneratedCodeAttrMarker,
									ExcludeFromCodeCoverageAttrMarker,
									NonUserCodeAttrMarker,
									NeverBrowsableAttrMarker)
					.WithBody(Block(List<StatementSyntax>([
						ExpressionStatement(InvocationExpression(IdentifierName(methodInfo.Name))
							.WithArgumentList(ArgumentList([
								Argument(IdentifierName("bindable").CastIfNeed(propertyInfo.TargetTypeName, CommonTypes.BindableObject)),
								Argument(IdentifierName("newValue").CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object))
							]))),
						ExpressionStatement(InvocationExpression(IdentifierName(methodInfo.Name))
							.WithArgumentList(ArgumentList([
								Argument(IdentifierName("bindable").CastIfNeed(propertyInfo.TargetTypeName, CommonTypes.BindableObject)),
								Argument(IdentifierName("oldValue").CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object)),
								Argument(IdentifierName("newValue").CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object))
							])))
					])));
			}
		}

		/// <summary>
		/// Generate code for ValidateValue
		/// </summary>
		/// <param name="hInfo"></param>
		/// <param name="propertyInfo"></param>
		/// <returns></returns>
		static IEnumerable<MemberDeclarationSyntax> GenerateValidateValueHandler(HierarchyInfo hInfo, AttachedPropertyInfo propertyInfo)
		{
			var validateMethodName = propertyInfo.ValidateMethodName;
			if (validateMethodName == null)
				yield break;

			// Get the property type syntax
			TypeSyntax parameterType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			if (propertyInfo.NeedGeneratePartialValidation)
			{
				// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
				// private partial bool <VALIDATE_METHOD_MAME>(<PROPERTY_TYPE> value);
				yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier(validateMethodName))
					.AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.PartialKeyword))
					.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
					.AddAttributeLists(GeneratedCodeAttrMarker)
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
			}
			//
			// var instance = (MyClass)bindable;
			//
			var instanceVarDeclaration = LocalDeclarationStatement(
				VariableDeclaration(ParseTypeName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier("_instance"))
					.WithInitializer(EqualsValueClause(
						CastExpression(IdentifierName(hInfo.MetadataName), IdentifierName("bindable"))
					)))));

			yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier(propertyInfo.Srv_ValidateValue))
				.AddModifiers(Token(SyntaxKind.StaticKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("bindable")).WithType(IdentifierName("BindableObject")),
					Parameter(Identifier("value")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
				.AddAttributeLists(GeneratedCodeAttrMarker,
								ExcludeFromCodeCoverageAttrMarker,
								NonUserCodeAttrMarker,
								NeverBrowsableAttrMarker)
				.WithBody(Block(List<StatementSyntax>([
					instanceVarDeclaration,
					ReturnStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(validateMethodName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("value").CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object))
						])))
				])));
		}

		/// <summary>
		/// Generate code for CoerceValue
		/// </summary>
		/// <param name="hInfo"></param>
		/// <param name="propertyInfo"></param>
		/// <returns></returns>
		static IEnumerable<MemberDeclarationSyntax> GenerateCoerceValueHandler(HierarchyInfo hInfo, AttachedPropertyInfo propertyInfo)
		{
			var coerceMethodName = propertyInfo.CoerceMethodName;
			if (coerceMethodName == null)
				yield break;

			// Get the property type syntax
			TypeSyntax parameterType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			if (propertyInfo.NeedGeneratePartialCoerce)
			{
				// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
				// private partial <PROPERTY_TYPE> <COERCE_METHOD_MAME>(<PROPERTY_TYPE> value);
				yield return MethodDeclaration(parameterType, Identifier(coerceMethodName))
					.AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.PartialKeyword))
					.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
					.AddAttributeLists(GeneratedCodeAttrMarker)
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
			}
			//
			// var instance = (MyClass)bindable;
			//
			var instanceVarDeclaration = LocalDeclarationStatement(
				VariableDeclaration(ParseTypeName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier("_instance"))
					.WithInitializer(EqualsValueClause(
						CastExpression(IdentifierName(hInfo.MetadataName), IdentifierName("bindable"))
					)))));

			yield return MethodDeclaration(PredefinedType(Token(SyntaxKind.ObjectKeyword)), Identifier(propertyInfo.Srv_CoerceValue))
				.AddModifiers(Token(SyntaxKind.StaticKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("bindable")).WithType(IdentifierName("BindableObject")),
					Parameter(Identifier("value")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword))))
				.AddAttributeLists(GeneratedCodeAttrMarker,
								ExcludeFromCodeCoverageAttrMarker,
								NonUserCodeAttrMarker,
								NeverBrowsableAttrMarker)
				.WithBody(Block(List<StatementSyntax>([
					instanceVarDeclaration,
					ReturnStatement(InvocationExpression(
						MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("_instance"),
							IdentifierName(coerceMethodName)))
						.WithArgumentList(ArgumentList([
							Argument(IdentifierName("value").CastIfNeed(propertyInfo.RealTypeName, CommonTypes.Object))
						])))
				])));
		}

		public static ImmutableArray<MemberDeclarationSyntax> GetOnPropertyChangeMethodsSyntax(HierarchyInfo hInfo, AttachedPropertyInfo propertyInfo)
		{
			var resultList = ImmutableArrayBuilder<MemberDeclarationSyntax>.Rent();

			resultList.AddRange(GenerateChangeHandlers(propertyInfo, propertyInfo.Srv_PropertyChanging, propertyInfo.ChangingMethodInfo).ToArray());
			resultList.AddRange(GenerateChangeHandlers(propertyInfo, propertyInfo.Srv_PropertyChanged, propertyInfo.ChangedMethodInfo).ToArray());
			resultList.AddRange(GenerateValidateValueHandler(hInfo, propertyInfo).ToArray());
			resultList.AddRange(GenerateCoerceValueHandler(hInfo, propertyInfo).ToArray());

			return resultList.ToImmutable();
		}
	}
}