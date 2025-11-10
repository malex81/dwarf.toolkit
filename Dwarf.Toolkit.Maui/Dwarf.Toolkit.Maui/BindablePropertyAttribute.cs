namespace Dwarf.Toolkit.Maui;

public sealed class BindablePropertyAttribute : Attribute
{
	public object? DefaultValue { get; set; }
	public string? DefaultValueExpression { get; set; }
}