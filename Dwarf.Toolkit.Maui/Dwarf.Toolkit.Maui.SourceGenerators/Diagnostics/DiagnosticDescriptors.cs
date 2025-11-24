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
}