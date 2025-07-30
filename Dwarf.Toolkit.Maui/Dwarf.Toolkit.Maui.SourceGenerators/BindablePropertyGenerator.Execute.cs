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
			// Matches a valid field declaration, for legacy support
			static bool IsCandidateField(SyntaxNode node, out TypeDeclarationSyntax? containingTypeNode)
			{
				// The node must represent a field declaration
				if (node is not VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: FieldDeclarationSyntax { AttributeLists.Count: > 0 } fieldNode } })
				{
					containingTypeNode = null;
					return false;
				}

				containingTypeNode = (TypeDeclarationSyntax?)fieldNode.Parent;
				return true;
			}

			// Check that the target is a valid field
			if (!IsCandidateField(node, out TypeDeclarationSyntax? parentNode))
			{
				return false;
			}

			// The candidate member must be in a type with a base type (as it must derive from ObservableObject)
			return parentNode?.IsTypeDeclarationWithOrPotentiallyWithBaseTypes<ClassDeclarationSyntax>() == true;
		}

		/// <summary>
		/// Checks whether a given candidate node is valid given a compilation.
		/// </summary>
		/// <param name="node">The <see cref="MemberDeclarationSyntax"/> instance to process.</param>
		/// <param name="semanticModel">The <see cref="SemanticModel"/> instance for the current run.</param>
		/// <returns>Whether <paramref name="node"/> is valid.</returns>
		public static bool IsCandidateValidForCompilation(MemberDeclarationSyntax node, SemanticModel semanticModel)
		{
			// At least C# 8 is always required
			if (!semanticModel.Compilation.HasLanguageVersionAtLeastEqualTo(LanguageVersion.CSharp11))
			{
				return false;
			}

			// If the target is a property, we only support using C# preview.
			// This is because the generator is relying on the 'field' keyword.
			//if (node is PropertyDeclarationSyntax && !semanticModel.Compilation.IsLanguageVersionPreview())
			//{
			//	return false;
			//}

			// All other cases are supported, the syntax filter is already validating that
			return true;
		}

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
			string fieldName = memberSymbol.Name;
			string propertyName = GetGeneratedPropertyName(memberSymbol);

			// Check for name collisions (only for fields)
			// If the generated property would collide, skip generating it entirely. This makes sure that
			// users only get the helpful diagnostic about the collision, and not the normal compiler error
			// about a definition for "Property" already existing on the target type, which might be confusing.
			if (fieldName == propertyName && memberSyntax.IsKind(SyntaxKind.FieldDeclaration))
			{
				propertyInfo = null;
				diagnostics = [];
				return false;
			}

			token.ThrowIfCancellationRequested();

			using ImmutableArrayBuilder<string> propertyChangedNames = ImmutableArrayBuilder<string>.Rent();

			// Get the nullability info for the property
			GetNullabilityInfo(
				memberSymbol,
				semanticModel,
				out bool isReferenceTypeOrUnconstrainedTypeParameter,
				out bool includeMemberNotNullOnSetAccessor);

			token.ThrowIfCancellationRequested();

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
				setterAccessibility,
				isReferenceTypeOrUnconstrainedTypeParameter,
				includeMemberNotNullOnSetAccessor);

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
		/// Gets the nullability info on the generated property
		/// </summary>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <param name="semanticModel">The <see cref="SemanticModel"/> instance for the current run.</param>
		/// <param name="isReferenceTypeOrUnconstraindTypeParameter">Whether the property type supports nullability.</param>
		/// <param name="includeMemberNotNullOnSetAccessor">Whether <see cref="MemberNotNullAttribute"/> should be used on the setter.</param>
		/// <returns></returns>
		private static void GetNullabilityInfo(
			ISymbol memberSymbol,
			SemanticModel semanticModel,
			out bool isReferenceTypeOrUnconstraindTypeParameter,
			out bool includeMemberNotNullOnSetAccessor)
		{
			// We're using IsValueType here and not IsReferenceType to also cover unconstrained type parameter cases.
			// This will cover both reference types as well T when the constraints are not struct or unmanaged.
			// If this is true, it means the field storage can potentially be in a null state (even if not annotated).
			isReferenceTypeOrUnconstraindTypeParameter = !GetPropertyType(memberSymbol).IsValueType;

			// Special case if the target member is a partial property. In this case, the type should always match the
			// declared type of the property declaration, and there is no need for the attribute on the setter. This
			// is because assigning the property in the constructor will directly assign to the backing field, and not
			// doing so from the constructor will cause Roslyn to emit a warning. Additionally, Roslyn can always see
			// that the backing field is being assigned from the setter, so the attribute is just never needed here.
			if (memberSymbol.Kind is SymbolKind.Property)
			{
				includeMemberNotNullOnSetAccessor = false;

				return;
			}

			// This is used to avoid nullability warnings when setting the property from a constructor, in case the field
			// was marked as not nullable. Nullability annotations are assumed to always be enabled to make the logic simpler.
			// Consider this example:
			//
			// partial class MyViewModel : ObservableObject
			// {
			//    public MyViewModel()
			//    {
			//        Name = "Bob";
			//    }
			//
			//    [ObservableProperty]
			//    private string name;
			// }
			//
			// The [MemberNotNull] attribute is needed on the setter for the generated Name property so that when Name
			// is set, the compiler can determine that the name backing field is also being set (to a non null value).
			// Of course, this can only be the case if the field type is also of a type that could be in a null state.
			includeMemberNotNullOnSetAccessor =
				isReferenceTypeOrUnconstraindTypeParameter &&
				GetPropertyType(memberSymbol).NullableAnnotation != NullableAnnotation.Annotated &&
				semanticModel.Compilation.HasAccessibleTypeWithMetadataName("System.Diagnostics.CodeAnalysis.MemberNotNullAttribute");
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
				return ImmutableArray<SyntaxKind>.Empty;
			}

			// We only allow a subset of all possible modifiers (aside from the accessibility modifiers)
			ReadOnlySpan<SyntaxKind> candidateKinds =
			[
				SyntaxKind.NewKeyword,
				SyntaxKind.VirtualKeyword,
				SyntaxKind.SealedKeyword,
				SyntaxKind.OverrideKeyword,
#if ROSLYN_4_3_1_OR_GREATER
                SyntaxKind.RequiredKeyword
#endif
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
		public static MemberDeclarationSyntax GetPropertySyntax(PropertyInfo propertyInfo)
		{
			using ImmutableArrayBuilder<StatementSyntax> setterStatements = ImmutableArrayBuilder<StatementSyntax>.Rent();

			string getterFieldIdentifierName;
			ExpressionSyntax getterFieldExpression;
			ExpressionSyntax setterFieldExpression;

			// If the annotated member is a partial property, we always use the 'field' keyword
			if (propertyInfo.AnnotatedMemberKind is SyntaxKind.PropertyDeclaration)
			{
				getterFieldIdentifierName = "field";
				getterFieldExpression = setterFieldExpression = IdentifierName(getterFieldIdentifierName);
			}
			else if (propertyInfo.FieldName == "value")
			{
				// In case the backing field is exactly named "value", we need to add the "this." prefix to ensure that comparisons and assignments
				// with it in the generated setter body are executed correctly and without conflicts with the implicit value parameter. We only need
				// to add "this." when referencing the field in the setter (getter and XML docs are not ambiguous)
				getterFieldIdentifierName = "value";
				getterFieldExpression = IdentifierName(getterFieldIdentifierName);
				setterFieldExpression = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ThisExpression(), (IdentifierNameSyntax)getterFieldExpression);
			}
			else if (SyntaxFacts.GetKeywordKind(propertyInfo.FieldName) != SyntaxKind.None ||
					 SyntaxFacts.GetContextualKeywordKind(propertyInfo.FieldName) != SyntaxKind.None)
			{
				// If the identifier for the field could potentially be a keyword, we must escape it.
				// This usually happens if the annotated field was escaped as well (eg. "@event").
				// In this case, we must always escape the identifier, in all cases.
				getterFieldIdentifierName = $"@{propertyInfo.FieldName}";
				getterFieldExpression = setterFieldExpression = IdentifierName(getterFieldIdentifierName);
			}
			else
			{
				getterFieldIdentifierName = propertyInfo.FieldName;
				getterFieldExpression = setterFieldExpression = IdentifierName(getterFieldIdentifierName);
			}

			if (propertyInfo.NotifyPropertyChangedRecipients || propertyInfo.IsOldPropertyValueDirectlyReferenced)
			{
				// Store the old value for later. This code generates a statement as follows:
				//
				// <PROPERTY_TYPE> __oldValue = <FIELD_EXPRESSIONS>;
				setterStatements.Add(
					LocalDeclarationStatement(
						VariableDeclaration(GetPropertyTypeForOldValue(propertyInfo))
						.AddVariables(
							VariableDeclarator(Identifier("__oldValue"))
							.WithInitializer(EqualsValueClause(setterFieldExpression)))));
			}

			// Add the OnPropertyChanging() call first:
			//
			// On<PROPERTY_NAME>Changing(value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changing"))
					.AddArgumentListArguments(Argument(IdentifierName("value")))));

			// Optimization: if the previous property value is not being referenced (which we can check by looking for an existing
			// symbol matching the name of either of these generated methods), we can pass a default expression and avoid generating
			// a field read, which won't otherwise be elided by Roslyn. Otherwise, we just store the value in a local as usual.
			ArgumentSyntax oldPropertyValueArgument = propertyInfo.IsOldPropertyValueDirectlyReferenced switch
			{
				true => Argument(IdentifierName("__oldValue")),
				false => Argument(LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword)))
			};

			// Also call the overload after that:
			//
			// On<PROPERTY_NAME>Changing(<OLD_PROPERTY_VALUE_EXPRESSION>, value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changing"))
					.AddArgumentListArguments(oldPropertyValueArgument, Argument(IdentifierName("value")))));

			// Gather the statements to notify dependent properties
			foreach (string propertyName in propertyInfo.PropertyChangingNames)
			{
				// This code generates a statement as follows:
				//
				// OnPropertyChanging(global::CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs.<PROPERTY_NAME>);
				setterStatements.Add(
					ExpressionStatement(
						InvocationExpression(IdentifierName("OnPropertyChanging"))
						.AddArgumentListArguments(Argument(MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("global::CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangingArgs"),
							IdentifierName(propertyName))))));
			}

			// Add the assignment statement:
			//
			// <FIELD_EXPRESSION> = value;
			setterStatements.Add(
				ExpressionStatement(
					AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						setterFieldExpression,
						IdentifierName("value"))));

			// If validation is requested, add a call to ValidateProperty:
			//
			// ValidateProperty(value, <PROPERTY_NAME>);
			if (propertyInfo.NotifyDataErrorInfo)
			{
				setterStatements.Add(
					ExpressionStatement(
						InvocationExpression(IdentifierName("ValidateProperty"))
						.AddArgumentListArguments(
							Argument(IdentifierName("value")),
							Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(propertyInfo.PropertyName))))));
			}

			// Add the OnPropertyChanged() call:
			//
			// On<PROPERTY_NAME>Changed(value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changed"))
					.AddArgumentListArguments(Argument(IdentifierName("value")))));

			// Do the same for the overload, as above:
			//
			// On<PROPERTY_NAME>Changed(<OLD_PROPERTY_VALUE_EXPRESSION>, value);
			setterStatements.Add(
				ExpressionStatement(
					InvocationExpression(IdentifierName($"On{propertyInfo.PropertyName}Changed"))
					.AddArgumentListArguments(oldPropertyValueArgument, Argument(IdentifierName("value")))));

			// Gather the statements to notify dependent properties
			foreach (string propertyName in propertyInfo.PropertyChangedNames)
			{
				// This code generates a statement as follows:
				//
				// OnPropertyChanging(global::CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs.<PROPERTY_NAME>);
				setterStatements.Add(
					ExpressionStatement(
						InvocationExpression(IdentifierName("OnPropertyChanged"))
						.AddArgumentListArguments(Argument(MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName("global::CommunityToolkit.Mvvm.ComponentModel.__Internals.__KnownINotifyPropertyChangedArgs"),
							IdentifierName(propertyName))))));
			}

			// Gather the statements to notify commands
			foreach (string commandName in propertyInfo.NotifiedCommandNames)
			{
				// This code generates a statement as follows:
				//
				// <COMMAND_NAME>.NotifyCanExecuteChanged();
				setterStatements.Add(
					ExpressionStatement(
						InvocationExpression(MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName(commandName),
							IdentifierName("NotifyCanExecuteChanged")))));
			}

			// Also broadcast the change, if requested
			if (propertyInfo.NotifyPropertyChangedRecipients)
			{
				// This code generates a statement as follows:
				//
				// Broadcast(__oldValue, value, "<PROPERTY_NAME>");
				setterStatements.Add(
					ExpressionStatement(
						InvocationExpression(IdentifierName("Broadcast"))
						.AddArgumentListArguments(
							Argument(IdentifierName("__oldValue")),
							Argument(IdentifierName("value")),
							Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(propertyInfo.PropertyName))))));
			}

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
			AccessorDeclarationSyntax setAccessor =
				AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
				.WithModifiers(propertyInfo.SetterAccessibility.ToSyntaxTokenList())
				.WithBody(Block(setterIfStatement));

			// Add the [MemberNotNull] attribute if needed:
			//
			// [MemberNotNull("<FIELD_NAME>")]
			// <SET_ACCESSOR>
			if (propertyInfo.IncludeMemberNotNullOnSetAccessor)
			{
				setAccessor = setAccessor.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.MemberNotNull"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(propertyInfo.FieldName)))))));
			}

			// Add the [RequiresUnreferencedCode] attribute if needed:
			//
			// [RequiresUnreferencedCode("The type of the current instance cannot be statically discovered.")]
			// <SET_ACCESSOR>
			if (propertyInfo.IncludeRequiresUnreferencedCodeOnSetAccessor)
			{
				setAccessor = setAccessor.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("The type of the current instance cannot be statically discovered.")))))));
			}

			// Also add any forwarded attributes
			setAccessor = setAccessor.AddAttributeLists(forwardedSetAccessorAttributes);

			// Construct the generated property as follows:
			//
			// <XML_SUMMARY>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			// <FORWARDED_ATTRIBUTES>
			// <PROPERTY_MODIFIERS> <FIELD_TYPE><NULLABLE_ANNOTATION?> <PROPERTY_NAME>
			// {
			//     <FORWARDED_ATTRIBUTES>
			//     <GETTER_ACCESSIBILITY> get => <FIELD_NAME>;
			//     <SET_ACCESSOR>
			// }
			return
				PropertyDeclaration(propertyType, Identifier(propertyInfo.PropertyName))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).FullName))),
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(BindablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
					AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage")))))
				.WithModifiers(GetPropertyModifiers(propertyInfo))
				.AddAccessorListAccessors(
					AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
					.WithModifiers(propertyInfo.GetterAccessibility.ToSyntaxTokenList())
					.WithExpressionBody(ArrowExpressionClause(getterFieldExpression))
					.WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
					setAccessor);
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
		/// Gets the <see cref="MemberDeclarationSyntax"/> instances for the <c>OnPropertyChanging</c> and <c>OnPropertyChanged</c> methods for the input field.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="PropertyInfo"/> instance to process.</param>
		/// <returns>The generated <see cref="MemberDeclarationSyntax"/> instances for the <c>OnPropertyChanging</c> and <c>OnPropertyChanged</c> methods.</returns>
		public static ImmutableArray<MemberDeclarationSyntax> GetOnPropertyChangeMethodsSyntax(PropertyInfo propertyInfo)
		{
			// Get the property type syntax
			TypeSyntax parameterType = IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> is changing.</summary>
			// /// <param name="value">The new property value being set.</param>
			// /// <remarks>This method is invoked right before the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changing(<PROPERTY_TYPE> value);
			MemberDeclarationSyntax onPropertyChangingDeclaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"On{propertyInfo.PropertyName}Changing"))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).FullName))),
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> is changing.</summary>"),
						Comment("/// <param name=\"value\">The new property value being set.</param>"),
						Comment($"/// <remarks>This method is invoked right before the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			// Get the type for the 'oldValue' parameter (which can be null on first invocation) 
			TypeSyntax oldValueTypeSyntax = GetPropertyTypeForOldValue(propertyInfo);

			// Construct the generated method as follows:
			//
			// /// <summary>Executes the logic for when <see cref="<PROPERTY_NAME>"/> is changing.</summary>
			// /// <param name="oldValue">The previous property value that is being replaced.</param>
			// /// <param name="newValue">The new property value being set.</param>
			// /// <remarks>This method is invoked right before the value of <see cref="<PROPERTY_NAME>"/> is changed.</remarks>
			// [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			// partial void On<PROPERTY_NAME>Changing(<OLD_VALUE_TYPE> oldValue, <PROPERTY_TYPE> newValue);
			MemberDeclarationSyntax onPropertyChanging2Declaration =
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"On{propertyInfo.PropertyName}Changing"))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("oldValue")).WithType(oldValueTypeSyntax),
					Parameter(Identifier("newValue")).WithType(parameterType))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).FullName))),
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
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
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"On{propertyInfo.PropertyName}Changed"))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(Parameter(Identifier("value")).WithType(parameterType))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).FullName))),
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
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
				MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier($"On{propertyInfo.PropertyName}Changed"))
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddParameterListParameters(
					Parameter(Identifier("oldValue")).WithType(oldValueTypeSyntax),
					Parameter(Identifier("newValue")).WithType(parameterType))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
						.AddArgumentListArguments(
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).FullName))),
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>Executes the logic for when <see cref=\"{propertyInfo.PropertyName}\"/> just changed.</summary>"),
						Comment("/// <param name=\"oldValue\">The previous property value that was replaced.</param>"),
						Comment("/// <param name=\"newValue\">The new property value that was set.</param>"),
						Comment($"/// <remarks>This method is invoked right after the value of <see cref=\"{propertyInfo.PropertyName}\"/> is changed.</remarks>")), SyntaxKind.OpenBracketToken, TriviaList())))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			return ImmutableArray.Create(
				onPropertyChangingDeclaration,
				onPropertyChanging2Declaration,
				onPropertyChangedDeclaration,
				onPropertyChanged2Declaration);
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

		/// <summary>
		/// Gets the <see cref="TypeSyntax"/> for the type of a given property, when it can possibly be <see langword="null"/>.
		/// </summary>
		/// <param name="propertyInfo">The input <see cref="PropertyInfo"/> instance to process.</param>
		/// <returns>The type of a given property, when it can possibly be <see langword="null"/></returns>
		private static TypeSyntax GetPropertyTypeForOldValue(PropertyInfo propertyInfo)
		{
			// For partial properties, the old value always matches the exact property type.
			// See additional notes for this in the 'GetNullabilityInfo' method above.
			if (propertyInfo.AnnotatedMemberKind is SyntaxKind.PropertyDeclaration)
			{
				return IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations);
			}

			// Prepare the nullable type for the previous property value. This is needed because if the type is a reference
			// type, the previous value might be null even if the property type is not nullable, as the first invocation would
			// happen when the property is first set to some value that is not null (but the backing field would still be so).
			// As a cheap way to check whether we need to add nullable, we can simply check whether the type name with nullability
			// annotations ends with a '?'. If it doesn't and the type is a reference type, we add it. Otherwise, we keep it.
			return propertyInfo.IsReferenceTypeOrUnconstrainedTypeParameter switch
			{
				true when !propertyInfo.TypeNameWithNullabilityAnnotations.EndsWith("?")
					=> IdentifierName($"{propertyInfo.TypeNameWithNullabilityAnnotations}?"),
				_ => IdentifierName(propertyInfo.TypeNameWithNullabilityAnnotations)
			};
		}

		/// <summary>
		/// Gets a <see cref="CompilationUnitSyntax"/> instance with the cached args of a specified type.
		/// </summary>
		/// <param name="containingTypeName">The name of the generated type.</param>
		/// <param name="argsTypeName">The argument type name.</param>
		/// <param name="names">The sequence of property names to cache args for.</param>
		/// <returns>A <see cref="CompilationUnitSyntax"/> instance with the sequence of cached args, if any.</returns>
		private static CompilationUnitSyntax? GetKnownPropertyChangingOrChangedArgsSyntax(
			string containingTypeName,
			string argsTypeName,
			ImmutableArray<string> names)
		{
			if (names.IsEmpty)
			{
				return null;
			}

			// This code takes a class symbol and produces a compilation unit as follows:
			//
			// // <auto-generated/>
			// #pragma warning disable
			// #nullable enable
			// namespace CommunityToolkit.Mvvm.ComponentModel.__Internals
			// {
			//     /// <summary>
			//     /// A helper type providing cached, reusable <see cref="<ARGS_TYPE_NAME>"/> instances
			//     /// for all properties generated with <see cref="global::CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute"/>.
			//     /// </summary>
			//     [global::System.CodeDom.Compiler.GeneratedCode("...", "...")]
			//     [global::System.Diagnostics.DebuggerNonUserCode]
			//     [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
			//     [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
			//     [global::System.Obsolete("This type is not intended to be used directly by user code")]
			//     internal static class <CONTAINING_TYPE_NAME>
			//     {
			//         <FIELDS>
			//     }
			// }
			return
				CompilationUnit().AddMembers(
				NamespaceDeclaration(IdentifierName("CommunityToolkit.Mvvm.ComponentModel.__Internals")).WithLeadingTrivia(TriviaList(
					Comment("// <auto-generated/>"),
					Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)),
					Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)))).AddMembers(
				ClassDeclaration(containingTypeName).AddModifiers(
					Token(SyntaxKind.InternalKeyword),
					Token(SyntaxKind.StaticKeyword)).AddAttributeLists(
						AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName($"global::System.CodeDom.Compiler.GeneratedCode"))
							.AddArgumentListArguments(
								AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).FullName))),
								AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(typeof(ObservablePropertyGenerator).Assembly.GetName().Version.ToString()))))))
						.WithOpenBracketToken(Token(TriviaList(
							Comment("/// <summary>"),
							Comment($"/// A helper type providing cached, reusable <see cref=\"{argsTypeName}\"/> instances"),
							Comment("/// for all properties generated with <see cref=\"global::CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute\"/>."),
							Comment("/// </summary>")), SyntaxKind.OpenBracketToken, TriviaList())),
						AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.DebuggerNonUserCode")))),
						AttributeList(SingletonSeparatedList(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage")))),
						AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName("global::System.ComponentModel.EditorBrowsable")).AddArgumentListArguments(
							AttributeArgument(ParseExpression("global::System.ComponentModel.EditorBrowsableState.Never"))))),
						AttributeList(SingletonSeparatedList(
							Attribute(IdentifierName("global::System.Obsolete")).AddArgumentListArguments(
							AttributeArgument(LiteralExpression(
								SyntaxKind.StringLiteralExpression,
								Literal("This type is not intended to be used directly by user code")))))))
					.AddMembers(names.Select(name => CreateFieldDeclaration(argsTypeName, name)).ToArray())))
				.NormalizeWhitespace();
		}

		/// <summary>
		/// Creates a field declaration for a cached property changing/changed name.
		/// </summary>
		/// <param name="fullyQualifiedTypeName">The field fully qualified type name (either <see cref="PropertyChangedEventArgs"/> or <see cref="PropertyChangingEventArgs"/>).</param>
		/// <param name="propertyName">The name of the cached property name.</param>
		/// <returns>A <see cref="FieldDeclarationSyntax"/> instance for the input cached property name.</returns>
		private static FieldDeclarationSyntax CreateFieldDeclaration(string fullyQualifiedTypeName, string propertyName)
		{
			// Create a static field with a cached property changed/changing argument for a specified property.
			// This code produces a field declaration as follows:
			//
			// /// <summary>The cached <see cref="<TYPE_NAME>"/> instance for all "<PROPERTY_NAME>" generated properties.</summary>
			// [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
			// [global::System.Obsolete("This field is not intended to be referenced directly by user code")]
			// public static readonly <ARG_TYPE> <PROPERTY_NAME> = new("<PROPERTY_NAME>");
			return
				FieldDeclaration(
				VariableDeclaration(IdentifierName(fullyQualifiedTypeName))
				.AddVariables(
					VariableDeclarator(Identifier(propertyName))
					.WithInitializer(EqualsValueClause(
						ObjectCreationExpression(IdentifierName(fullyQualifiedTypeName))
						.AddArgumentListArguments(Argument(
							LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(propertyName))))))))
				.AddModifiers(
					Token(SyntaxKind.PublicKeyword),
					Token(SyntaxKind.StaticKeyword),
					Token(SyntaxKind.ReadOnlyKeyword))
				.AddAttributeLists(
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.ComponentModel.EditorBrowsable")).AddArgumentListArguments(
						AttributeArgument(ParseExpression("global::System.ComponentModel.EditorBrowsableState.Never")))))
					.WithOpenBracketToken(Token(TriviaList(
						Comment($"/// <summary>The cached <see cref=\"{fullyQualifiedTypeName}\"/> instance for all \"{propertyName}\" generated properties.</summary>")),
						SyntaxKind.OpenBracketToken, TriviaList())),
					AttributeList(SingletonSeparatedList(
						Attribute(IdentifierName("global::System.Obsolete")).AddArgumentListArguments(
						AttributeArgument(LiteralExpression(
							SyntaxKind.StringLiteralExpression,
							Literal("This field is not intended to be referenced directly by user code")))))));
		}

		/// <summary>
		/// Get the generated property name for an input field or property.
		/// </summary>
		/// <param name="memberSymbol">The input <see cref="ISymbol"/> instance to process.</param>
		/// <returns>The generated property name for <paramref name="memberSymbol"/>.</returns>
		public static string GetGeneratedPropertyName(ISymbol memberSymbol)
		{
			// If the input is a property, just always match the name exactly
			if (memberSymbol is IPropertySymbol propertySymbol)
			{
				return propertySymbol.Name;
			}

			string propertyName = memberSymbol.Name;

			if (propertyName.StartsWith("m_"))
			{
				propertyName = propertyName[2..];
			}
			else if (propertyName.StartsWith("_"))
			{
				propertyName = propertyName.TrimStart('_');
			}

			return $"{char.ToUpper(propertyName[0], CultureInfo.InvariantCulture)}{propertyName[1..]}";
		}
	}
}