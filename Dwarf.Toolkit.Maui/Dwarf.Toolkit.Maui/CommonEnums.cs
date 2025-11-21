namespace Dwarf.Toolkit.Maui;
/// <summary>
/// This enum is introduced to avoid direct reference to Microsoft.Maui assemblies
/// </summary>
public enum BindingModeDef
{
	Default = 0,
	TwoWay = 1,
	OneWay = 2,
	OneWayToSource = 3,
	OneTime = 4
}