using System.ComponentModel;
using System.Linq.Expressions;

namespace Dwarf.Toolkit.Basic.LinqBinder;

public static class NotifyPropertyChangedExtensions
{
	public static BindingContent<TSrc> CreateBindingContent<TSrc>(this TSrc source) where TSrc : INotifyPropertyChanged
		=> new(source);

	public static BindingNode<TSrc, TVal> BindProperty<TSrc, TVal>(this TSrc source, Expression<Func<TSrc, TVal>> path) where TSrc : INotifyPropertyChanged
		=> new(source, path);
}
