using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class ExampleBindableObject : BindableObject
{
	public record CustomType(int Num, string Text);

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

	[BindableProperty(DefaultValueExpression = "new CustomType(12, \"Настя и Даша\")", ValidateMethod = "ValidateCustomType")]
	public partial CustomType CustomProp { get; set; }

	private bool ValidateCustomType(CustomType value)
	{
		return true;
	}

	[BindableProperty(DefaultValue = "Здравствуй, товарищь")]
	public partial string TextProp { get; set; }

	[BindableProperty(ValidateMethod = nameof(ValidateNumProp))]
	internal partial int NumProp { get; set; }

	private bool ValidateNumProp(int value)
	{
		if (value > 5)
		{
			CustomProp = CustomProp with { Num = value };
			return false;
		}
		return true;
	}

	void OnNumPropChanged(int val)
	{
	}
}