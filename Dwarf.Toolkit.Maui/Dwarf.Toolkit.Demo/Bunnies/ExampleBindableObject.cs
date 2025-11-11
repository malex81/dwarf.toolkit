using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class ExampleBindableObject : BindableObject
{
	#region Example property
	public static readonly BindableProperty ExampleProperty = BindableProperty.Create(nameof(Example), typeof(int), typeof(ExampleBindableObject), defaultValue: 11);

	public int Example
	{
		get => (int)GetValue(ExampleProperty);
		set => SetValue(ExampleProperty, value);
	}
	#endregion

	public ExampleBindableObject()
	{
	}

	[BindableProperty(DefaultValueExpression = "typeof(ExampleBindableObject)")]
	public partial string TextProp { get; set; }

	[BindableProperty(DefaultValueExpression = "24")]
	partial int NumProp { get; set; }

}