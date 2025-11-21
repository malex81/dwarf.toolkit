using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

internal partial class ExampleBindableObject : BindableObject
{
	public record CustomType(int Num, string Text);

	#region Example property
	public static readonly BindableProperty ExampleProperty
		= BindableProperty.Create(nameof(Example), typeof(string), typeof(ExampleBindableObject), defaultValue: null, propertyChanged: Example_Changed);

	static void Example_Changed(BindableObject bindable, object oldValue, object newValue)
	{
		var _instance = (ExampleBindableObject)bindable;
		var v0 = (string)oldValue;
		var v1 = (string)newValue;
		_instance.OnExampleChanged(v1);
	}

	void OnExampleChanged(string? value)
	{
	}

	public string? Example
	{
		get => (string)GetValue(ExampleProperty);
		set => SetValue(ExampleProperty, value);
	}
	#endregion

	//public static partial string? GetOuterText(Label target);

	public ExampleBindableObject()
	{
		var example = Example;
		Example = "12321";
		Example = null;
		example = Example;
	}

	[BindableProperty(DefaultValueExpression = "new CustomType(12, \"Настя и Даша\")", ValidateMethod = "ValidateCustomType")]
	public partial CustomType CustomProp { get; set; }

	private bool ValidateCustomType(CustomType value)
	{
		return true;
	}

	[BindableProperty]
	partial object ObjProp { get; set; }

	partial void OnObjPropChanged(object oldValue, object newValue)
	{
	}

	[BindableProperty(DefaultValue = "Здравствуй, товарищь", DefaultBindingMode = BindingModeDef.OneTime)]
	public partial string? TextProp { get; set; }

	[BindableProperty(ValidateMethod = nameof(ValidateNumProp), CoerceMethod = "CoerceNumProp")]
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

	private int CoerceNumProp(int value)
	{
		return value + 1;
	}

	void OnNumPropChanged(int val)
	{
	}
}