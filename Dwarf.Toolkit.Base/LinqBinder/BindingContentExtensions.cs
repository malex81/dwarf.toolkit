using Dwarf.Toolkit.Base.SystemExtension;

namespace Dwarf.Toolkit.Base.LinqBinder;

public static class BindingContentExtensions
{
	public static BindingContent<T> ClearOnDispose<T>(this BindingContent<T> bindingContent, ICollection<IDisposable> dispList) where T : notnull
	{
		dispList.Add(DisposableHelper.FromAction(bindingContent.Clear));
		return bindingContent;
	}
}
