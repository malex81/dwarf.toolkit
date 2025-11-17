namespace Dwarf.Toolkit.Maui;

/// <summary>
/// This enum is introduced to avoid direct reference to Microsoft.Maui assemblies
/// </summary>
public enum BindingModeDef { Default = 0, TwoWay = 1, OneWay = 2, OneWayToSource = 3, OneTime = 4 }

[AttributeUsage(AttributeTargets.Property)]
public sealed class BindablePropertyAttribute : Attribute
{
	public object? DefaultValue { get; set; }
	public string? DefaultValueExpression { get; set; }
	public BindingModeDef DefaultBindingMode { get; set; }
	/// <summary>
	/// The neme of method for handle prperty changing.
	/// If not set, a method On<PropertyName>Changing will be generated
	/// </summary>
	public string? ChangingMethod { get; set; }
	/// <summary>
	/// The neme of method for handle prperty changed.
	/// If not set, a method On<PropertyName>Changed will be generated
	/// </summary>
	public string? ChangedMethod { get; set; }
	/// <summary>
	/// The neme of method for validate value.
	/// </summary>
	public string? ValidateMethod { get; set; }
	/// <summary>
	/// The neme of method for coerce value.
	/// </summary>
	public string? CoerceMethod { get; set; }
}