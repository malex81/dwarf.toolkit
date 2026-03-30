using Microsoft.CodeAnalysis;
using System.ComponentModel;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics;

internal static class DiagnosticDescriptors
{
	const string DefaultCategory = "BindablePropertyGenerator";

	public static readonly DiagnosticDescriptor DefaultValueExprassionWithDefaultValue_Warning = new(
		id: "DTKM0001",
		title: "DefaultValueExprassion used with DefaultValue",
		messageFormat: "Propery {0} use [DefaultValueExpression] simultaneously with [DefaultValue]",
		category: DefaultCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Cannot use [DefaultValueExpression] simultaneously with [DefaultValue].");

	public static readonly DiagnosticDescriptor InvalidGetMethodForAttachedProperty_Error = new(
		id: "DTKM0002",
		title: "Invalid method notation used with [AttachedProperty] attribute",
		messageFormat: "Method {0} has invalid {1}",
		category: DefaultCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "[AttachedProperty] attribute must be used with method of the following signature: public static partial <PropertyType> Get<PropertyName>(BindableObject target);.");

	public static readonly DiagnosticDescriptor InvalidPropertyDeclarationIsNotIncompletePartial_Error = new(
		id: "DTKM0010",
		title: "Using [BindableProperty] invalid property declaration",
		messageFormat: """The property {0}.{1} is not an incomplete partial definition ([BindableProperty] must be used on partial property definitions with no implementation part)""",
		category: DefaultCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A property using [BindableProperty] is not an incomplete partial definition part ([BindableProperty] must be used on partial property definitions with no implementation part).");

	public static readonly DiagnosticDescriptor InvalidPropertyDeclarationReturnsByRef_Error = new(
		id: "DTKM0011",
		title: "Using [BindableProperty] on a property that returns byref",
		messageFormat: """The property {0}.{1} returns a ref value ([BindableProperty] must be used on properties returning a type by value)""",
		category: DefaultCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A property using [BindableProperty] returns a value by reference ([BindableProperty] must be used on properties returning a type by value).");

	public static readonly DiagnosticDescriptor InvalidPropertyDeclarationReturnsRefLikeType_Error = new(
		id: "DTKM0012",
		title: "Using [BindableProperty] on a property that returns byref-like",
		messageFormat: """The property {0}.{1} returns a byref-like value ([BindableProperty] must be used on properties of a non byref-like type)""",
		category: DefaultCategory,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "A property using [BindableProperty] returns a byref-like value ([BindableProperty] must be used on properties of a non byref-like type).");

}