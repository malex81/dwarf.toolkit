using Microsoft.CodeAnalysis;
using System.ComponentModel;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics;

internal static class DiagnosticDescriptors
{
	public static readonly DiagnosticDescriptor DefaultValueExprassionAndDefaultValueWarning = new(
		id: "BPMTK0001",
		title: "DefaultValueExprassion used with DefaultValue",
		messageFormat: "Cannot use <DefaultValueExprassion> at the same time as <DefaultValue>",
		category: typeof(BindablePropertyGenerator).FullName,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Cannot use <DefaultValueExprassion> at the same time as <DefaultValue>.");
}