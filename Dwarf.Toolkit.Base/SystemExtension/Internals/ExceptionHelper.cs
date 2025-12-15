using System.Diagnostics.CodeAnalysis;

namespace Dwarf.Toolkit.Base.SystemExtension.Internals;

internal static class ExceptionHelper
{
	public static void ThrowIfNull([NotNull] object value)
	{
		if (value == null) throw new ArgumentNullException("value");
	}
}