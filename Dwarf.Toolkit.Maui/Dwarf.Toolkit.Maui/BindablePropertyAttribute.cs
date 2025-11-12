namespace Dwarf.Toolkit.Maui;

[AttributeUsage(AttributeTargets.Property)]
public sealed class BindablePropertyAttribute : Attribute
{
	public object? DefaultValue { get; set; }
	public string? DefaultValueExpression { get; set; }
}