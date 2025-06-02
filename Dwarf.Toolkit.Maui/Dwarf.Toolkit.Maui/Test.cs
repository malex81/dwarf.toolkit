using Dwarf.Toolkit.Base.SystemExtension;

namespace Dwarf.Toolkit.Maui;

public class Test : IDisposable
{
	readonly DisposableList dispList = [];

	public void Dispose()
	{
		dispList.Dispose();
	}
}