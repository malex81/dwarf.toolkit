namespace Dwarf.Toolkit.Base.SystemExtension;

public class DisposableList : List<IDisposable>, IDisposable
{
	public DisposableList() : base() { }
	public DisposableList(IEnumerable<IDisposable> source) : base(source) { }

	public void Dispose()
	{
		this.DisposeAll();
		Clear();
		GC.SuppressFinalize(this);
	}
}
