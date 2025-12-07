# Dwarf.Toolkit.Maui

Helper for MAUI BindablePropery

- [Nuget package](https://www.nuget.org/packages/Dwarf.Toolkit.Maui/)
- [Source code](https://github.com/malex81/dwarf.toolkit/tree/main/Dwarf.Toolkit.Maui)
- [Documentation](https://github.com/malex81/dwarf.toolkit/blob/main/Dwarf.Toolkit.Maui/README.md)

## BindableProperty Generator

To generate a MAUI `BindableProperty`, apply the `BindableProperty` attribute to the partial property, as in the example below:

```c#
internal partial class ExampleBindableObject : BindableObject
{
	[BindableProperty(DefaultValue = "Hello world ", DefaultBindingMode = BindingModeDef.OneTime)]
	partial string TextProp { get; set; }

	[BindableProperty(DefaultValue = 18, ValidateMethod = nameof(ValidateNumProp))]
	partial int NumProp { get; set; }
}
```

This short snippet will generate the code for `BindableProperty.Create( ... )`, property wrapper like this:

```c#
partial string TextProp
{
	get => (string)GetValue(TextPropProperty);
	set => SetValue(TextPropProperty, value);
}
```

And necessary triggers for  `PropertyChanging`, `PropertyChanged`, `ValidateValue` and `CoerceValue`.

### Supported arguments

| Name                   | Description       |
|------------------------|-------------------|
| **DefaultValue**           | The value that passed directly to `defaultValue` of `BindableProperty.Create` method. |
| **DefaultValueExpression** | An expression, represented as a string, that the generator will extract and passed to `defaultValue` of `BindableProperty.Create` method. |
| **DefaultBindingMode**     | The `defaultBindingMode`. To avoid direct reference to Microsoft.Maui ecosystem, local enum `BindingModeDef` is added. Generator translate it value to `Microsoft.Maui.Controls.BindingMode`. |
| **ChangingMethod**         | The name of method for property changing callback. This method not static and takes one (new value) or two (old value and new value) parameters of correct property type. The generator will prepare static handler and perform the necessary type casts. If this argument is not set, generator will create partial method with `On<PropertyName>Changing` name. This method can be implemented in the future. |
| **ChangedMethod**          | The name of method for property changed callback. This method not static and takes one (new value) or two (old value and new value) parameters of correct property type. The generator will prepare static handler and perform the necessary type casts. If this argument is not set, generator will create partial method with `On<PropertyName>Changed` name. This method can be implemented in the future. |
| **ValidateMethod**         | The name of method for validate value. This method not static, takes one parameter of property type and returns boolean result. The generator will prepare static handler and perform the necessary type casts. |
| **CoerceMethod**           | The name of method for coerce value. This method not static, takes one parameter of property type and returns a new value of the same type. The generator will prepare static handler and perform the necessary type casts. |


## AttachedProperty Generator

To generate an MAUI attached property, apply the `AttachedProperty` attribute to the static partial method conform to the signature `Get<PropertyName>(...)`, as in the example below:

```c#
public static partial class ExampleObject
{
	[AttachedProperty(DefaultValue = "Hello world", CoerceMethod = "CoerceExampleText", ValidateMethod = "ValidateExampleText")]
	public static partial string? GetExampleText(BindableObject target);
}
```

This snippet will generate the code for `BindableProperty.CreateAttached( ... )`, implementation of get-method, set-method and necessary triggers for  `PropertyChanging`, `PropertyChanged`, `ValidateValue` and `CoerceValue`. For this example, the following code elements will be generated:

```c#
/// <summary>
/// Creates an attached property named ExampleText, of type <see cref="string"/>
/// </summary>
public static readonly global::Microsoft.Maui.Controls.BindableProperty ExampleTextProperty = global::Microsoft.Maui.Controls.BindableProperty.CreateAttached("ExampleText", typeof(string), typeof(ExampleObject), "Hello world", validateValue: __ExampleText_Validate, coerceValue: __ExampleText_Coerce);
...
public static partial string? GetExampleText(global::Microsoft.Maui.Controls.BindableObject target) => (string)target.GetValue(ExampleTextProperty);
...
public static void SetExampleText(global::Microsoft.Maui.Controls.BindableObject target, string? value) => target.SetValue(ExampleTextProperty, value);
...
static partial void OnExampleTextChanging(global::Microsoft.Maui.Controls.BindableObject target, string? value);
static partial void OnExampleTextChanging(global::Microsoft.Maui.Controls.BindableObject target, string? oldValue, string? newValue);
static partial void OnExampleTextChanged(global::Microsoft.Maui.Controls.BindableObject target, string? value);
static partial void OnExampleTextChanged(global::Microsoft.Maui.Controls.BindableObject target, string? oldValue, string? newValue);

private static partial bool ValidateExampleText(global::Microsoft.Maui.Controls.BindableObject target, string? value);
static bool __ExampleText_Validate(global::Microsoft.Maui.Controls.BindableObject bindable, object value) => ValidateExampleText(bindable, (string)value);

private static partial string? CoerceExampleText(global::Microsoft.Maui.Controls.BindableObject target, string? value);
static object __ExampleText_Coerce(global::Microsoft.Maui.Controls.BindableObject bindable, object value) => CoerceExampleText(bindable, (string)value);

```

### Supported arguments

| Name                   | Description       |
|------------------------|-------------------|
| **DefaultValue**           | The value that passed directly to `defaultValue` of `BindableProperty.CreateAttached` method. |
| **DefaultValueExpression** | An expression, represented as a string, that the generator will extract and passed to `defaultValue` of `BindableProperty.CreateAttached` method. |
| **DefaultBindingMode**     | The `defaultBindingMode`. To avoid direct reference to Microsoft.Maui ecosystem, local enum `BindingModeDef` is added. Generator translate it value to `Microsoft.Maui.Controls.BindingMode`. |
| **ChangingMethod**         | The name of method for property changing callback. This method is static and takes two (target object and new value) or three (target object, old value and new value) parameters of correct property type. The generator will prepare static handler and perform the necessary type casts. If this argument is not set, generator will create partial method with `On<PropertyName>Changing` name. This method can be implemented in the future. |
| **ChangedMethod**          | The name of method for property changed callback. This method is static and takes two (target object and new value) or three (target object, old value and new value) parameters of correct property type. The generator will prepare static handler and perform the necessary type casts. If this argument is not set, generator will create partial method with `On<PropertyName>Changed` name. This method can be implemented in the future. |
| **ValidateMethod**         | The name of method for validate value. This method is static, takes two parameters (target object and property value) and returns boolean result. The generator will prepare static handler and perform the necessary type casts. |
| **CoerceMethod**           | The name of method for coerce value. This method is static, takes two parameters (target object and property value) and returns a new value of the same type. The generator will prepare static handler and perform the necessary type casts. |


## Change Log

### 0.1.4 - 2025.12.07

- Add attached property support
- Fix some little bugs

### 0.1.3 - 2025.11.19

- Fix nullable annotation of reference types
- Add some attributes to mark service methods

### 0.1.2 - 20125.11.18

- Add `DefaultBindingMode`, `ChangingMethod`, `ChangedMethod`, `ValidateMethod` and `CoerceMethod` properties to `BindablePropertyAttribute`

### 0.1.1 - 2025.11.12

- Add `DefaultValue` and `DefaultValueExpression` properties to `BindablePropertyAttribute`
- Fix property accessibility

### 0.1.0 - 2025.10.31

The `Dwarf.Toolkit.Maui.SourceGenerators` generate BindableProperty. Currently, there are no additional parameters.

