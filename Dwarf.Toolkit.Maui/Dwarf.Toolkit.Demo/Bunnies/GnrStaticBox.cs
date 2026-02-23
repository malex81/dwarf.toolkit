using Dwarf.Toolkit.Maui;

namespace Dwarf.Toolkit.Demo.Bunnies;

public static partial class GnrStaticBox<T>
{
	public static partial class StaticSubClass<A, B>
	{
		[AttachedProperty()]
		public static partial T GetSomething(Label target);
	}

	#region Example attached property
	public static readonly BindableProperty GlyphProperty =
	   BindableProperty.CreateAttached("RegularGlyph", typeof(T), typeof(GnrStaticBox<T>), default(T), propertyChanged: OnGlyphChanged);

	public static T GetGlyph(Label view) => (T)view.GetValue(GlyphProperty);
	public static void SetGlyph(Label view, T value) => view.SetValue(GlyphProperty, value);

	static void OnGlyphChanged(BindableObject view, object oldValue, object newValue)
	{
		if (newValue is not T glyph)
			return;
	}
	#endregion

}