namespace Dwarf.Toolkit.Basic.DiHelpers;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class FactoryMethodAttribute(Type? resultType = null) : Attribute
{
	public Type? ResultType { get; } = resultType;
}
