namespace Dwarf.Toolkit.Basic.LinqBinder.Binders;

internal static class ReflectionHelper
{
	public static object? GetDefault(this Type type)
	{
		return type.IsValueType ? Activator.CreateInstance(type) : null;
	}
}
