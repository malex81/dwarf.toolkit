using Microsoft.CodeAnalysis;
using System.ComponentModel;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics;

internal static class DiagnosticDescriptors
{
	public static readonly DiagnosticDescriptor DefaultValueExprassionWithDefaultValue_Warning = new(
		id: "DTKM0001",
		title: "DefaultValueExprassion used with DefaultValue",
		messageFormat: "Propery {0} use [DefaultValueExpression] simultaneously with [DefaultValue]",
		category: typeof(BindablePropertyGenerator).FullName,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Cannot use [DefaultValueExpression] simultaneously with [DefaultValue].");
}