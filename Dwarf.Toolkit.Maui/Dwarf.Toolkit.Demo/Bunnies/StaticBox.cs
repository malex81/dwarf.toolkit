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

	#endregion
}