# Dwarf.Toolkit.Maui

Set of helpers for MAUI

[Nuget](https://www.nuget.org/packages/Dwarf.Toolkit.Maui/)

## BindableProperty Generator

Generate MAUI BindableProperty

Example of use:
```c#
internal partial class ExampleBindableObject : BindableObject
{
	public SimpleBindableObject()
	{
	}

	[BindableProperty]
	partial string TextProp { get; set; }

	[BindableProperty(DefaultValue = 18)]
	partial int NumProp { get; set; }
}
```

This example will generate the following code

```c#
/// <inheritdoc/>
partial class ExampleBindableObject
{
	public static readonly BindableProperty TextPropProperty = BindableProperty.Create(nameof(TextProp), typeof(string), typeof(ExampleBindableObject));
	/// <inheritdoc/>
	[global::System.CodeDom.Compiler.GeneratedCode("Dwarf.Toolkit.Maui.SourceGenerators.BindablePropertyGenerator", "0.1.1.0")]
	[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	partial string TextProp { get => (string)GetValue(TextPropProperty); set => SetValue(TextPropProperty, value); }

	public static readonly BindableProperty NumPropProperty = BindableProperty.Create(nameof(NumProp), typeof(int), typeof(ExampleBindableObject), defaultValue: 18);
	/// <inheritdoc/>
	[global::System.CodeDom.Compiler.GeneratedCode("Dwarf.Toolkit.Maui.SourceGenerators.BindablePropertyGenerator", "0.1.1.0")]
	[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
	partial int NumProp { get => (int)GetValue(NumPropProperty); set => SetValue(NumPropProperty, value); }
}
```

## Change Log

### 0.1.1 - 2025.11.12

- Add `DefaultValue` and `DefaultValueExpression` properties to `BindablePropertyAttribute`
- Fix property accessibility

### 0.1.0 - 2025.10.31

The `Dwarf.Toolkit.Maui.SourceGenerators` generate BindableProperty. Currently, there are no additional parameters.

