using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class SimpleBindableObject : BindableObject
{
	[BindableProperty]
	partial string PropA { get; set; }
}