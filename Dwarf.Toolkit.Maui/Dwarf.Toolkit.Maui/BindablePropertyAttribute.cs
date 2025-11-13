namespace Dwarf.Toolkit.Maui;

[AttributeUsage(AttributeTargets.Property)]
public sealed class BindablePropertyAttribute : Attribute
{
	public object? DefaultValue { get; set; }
	public string? DefaultValueExpression { get; set; }
	public string? ChangingMethod { get; set; }
	public string? ChangedMethod { get; set; }
	public string? ValidateMethod { get; set; }
	public string? CoerceMethod { get; set; }
}