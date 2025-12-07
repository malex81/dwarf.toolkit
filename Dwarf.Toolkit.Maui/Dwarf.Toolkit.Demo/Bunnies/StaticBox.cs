using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

public enum FARegularGlyphs
{
	None = 0,
	CirclePlay = 0xf144,
	CircleStop = 0xf28d,
	CirclePause = 0xf28b,
	Star = 0xf005,
	Heart = 0xf004,
	Bookmark = 0xf02e,
	CircleQuestion = 0xf059,
	CircleXMark = 0xf057,
	Lightbulb = 0xf0eb,
	TrashCan = 0xf2ed,
	SquareCheck = 0xf14a,
	CircleCheck = 0xf058,
}

internal static partial class StaticBox
{
	#region Example attached property

	public static readonly BindableProperty RegularGlyphProperty =
	   BindableProperty.CreateAttached("RegularGlyph", typeof(FARegularGlyphs), typeof(StaticBox), FARegularGlyphs.None, propertyChanged: OnRegularGlyphChanged);

	public static FARegularGlyphs GetRegularGlyph(Label view) => (FARegularGlyphs)view.GetValue(RegularGlyphProperty);
	public static void SetRegularGlyph(Label view, FARegularGlyphs value) => view.SetValue(RegularGlyphProperty, value);

	static void OnRegularGlyphChanged(BindableObject view, object oldValue, object newValue)
	{
		if (newValue is not FARegularGlyphs glyph || glyph == FARegularGlyphs.None)
			return;
	}

	const string DefaultOuterText = "Первичное значение";

	[AttachedProperty(DefaultValueExpression = nameof(DefaultOuterText), CoerceMethod = "CoerceOuterText", ValidateMethod = "ValidateOuterText")]
	public static partial string? GetOuterText(BindableObject target);

	static partial void OnOuterTextChanged(BindableObject target, string? oldValue, string? newValue)
	{
	}

	private static partial bool ValidateOuterText(BindableObject target, string? value)
	{
		return value != null && !string.IsNullOrEmpty(value);
	}

	private static partial string? CoerceOuterText(BindableObject target, string? value)
	{
		if(value != DefaultOuterText) return value + " - новое значение";
		return value;
	}

	//[AttachedProperty(DefaultValue = "Hello world", CoerceMethod = "CoerceExampleText", ValidateMethod = "ValidateExampleText")]
	//public static partial string? GetExampleText(BindableObject target);

	#endregion
}