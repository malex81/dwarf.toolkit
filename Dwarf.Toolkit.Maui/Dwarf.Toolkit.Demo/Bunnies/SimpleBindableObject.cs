using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class SimpleBindableObject : BindableObject
{
	#region Example property
	public static readonly BindableProperty ExampleProperty = BindableProperty.Create(nameof(Example), typeof(int), typeof(SimpleBindableObject), defaultValue: 11);

	public int Example
	{
		get => (int)GetValue(ExampleProperty);
		set => SetValue(ExampleProperty, value);
	}
	#endregion

	public SimpleBindableObject()
	{
	}

	[BindableProperty]
	partial string PropA { get; set; }

	[BindableProperty(DefaultValue = 18)]
	partial int PropB { get; set; }

}