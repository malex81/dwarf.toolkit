namespace Dwarf.Toolkit.Maui;
/// <summary>
/// Attribute must be applied to a static partial method with signature
/// [public] static partial <valueType> GetPropertyName(BindableObject target);
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AttachedPropertyAttribute : Attribute
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