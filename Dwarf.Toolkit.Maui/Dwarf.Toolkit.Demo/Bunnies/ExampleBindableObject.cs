using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class ExampleBindableObject : BindableObject
{
	#region Example property
	public static readonly BindableProperty ExampleProperty
		= BindableProperty.Create(nameof(Example), typeof(int), typeof(ExampleBindableObject), defaultValue: 11, propertyChanged: Example_Changed);

	static void Example_Changed(BindableObject bindable, object oldValue, object newValue)
	{
		var _instance = (ExampleBindableObject)bindable;
		//_instance.OnNumPropChanged((int)newValue);
		//_instance.OnNumPropChanged((int)oldValue, (int)newValue);
	}

	public int Example
	{
		get => (int)GetValue(ExampleProperty);
		set => SetValue(ExampleProperty, value);
	}
	#endregion

	public ExampleBindableObject()
	{
	}

	[BindableProperty(DefaultValueExpression = "nameof(ExampleBindableObject)")]
	public partial string TextProp { get; set; }

	[BindableProperty(ValidateMethod = "ValidateNumProp")]
	internal partial int NumProp { get; set; }

	private partial bool ValidateNumProp(int value)
	{
		return true;
	}

	void OnNumPropChanged(int val)
	{
	}
}