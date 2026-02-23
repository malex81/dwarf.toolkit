using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal sealed partial class GnrBindableObject<A, B> : BindableObject
{
	public partial class BindableSubClass<C, D> : BindableObject
	{
		[BindableProperty(DefaultValue = "Здравствуй, товарищь", DefaultBindingMode = BindingModeDef.OneTime)]
		public partial string? TextProp { get; set; }

		partial void OnTextPropChanged(string? value)
		{
		}
	}
}
