namespace Dwarf.Toolkit.Maui;

[AttributeUsage(AttributeTargets.Property)]
public sealed class BindablePropertyAttribute : Attribute
{
	public object? DefaultValue { get; set; }
	public string? DefaultValueExpression { get; set; }
	/// <summary>
	/// The neme of method for handle prperty changing.
	/// If not set, a method On<PropertyName>Changing will be generated
	public string? ChangingMethod { get; set; }
	/// <summary>
	/// The neme of method for handle prperty changed.
	/// If not set, a method On<PropertyName>Changed will be generated
	public string? ChangedMethod { get; set; }
	/// <summary>
	/// The neme of method for validate value.
	public string? ValidateMethod { get; set; }
	/// <summary>
	/// The neme of method for coerce value.
	public string? CoerceMethod { get; set; }
}