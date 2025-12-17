using Dwarf.Toolkit.Basic.DiHelpers;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
	#region Batch services
	public static IServiceCollection TryAddBatch<TB>(this IServiceCollection services, TB batch, Action<IServiceCollection, TB> add)
		where TB : class
	{
		if (ServicesBatchConfig.GetConfig(services).AddBatchRequest(batch))
			add(services, batch);
		return services;
	}

	public static IServiceCollection AddBatch(this IServiceCollection services, IServicesBatch batch)
		=> services.TryAddBatch(batch, (s, b) => b.Configure(s));
	public static IServiceCollection AddBatch<TB>(this IServiceCollection services) where TB : IServicesBatch, new()
		=> services.AddBatch(new TB());
	public static IServiceCollection AddBatch<TP>(this IServiceCollection services, IServicesBatch<TP> batch, TP prm)
		=> services.TryAddBatch(batch, (s, b) => b.Configure(s, prm));
	public static IServiceCollection AddBatch<TB, TP>(this IServiceCollection services, TP prm) where TB : IServicesBatch<TP>, new()
		=> services.AddBatch(new TB(), prm);
	#endregion

	#region Internal helpers
	class ImplHolder<T>
	{
		public ImplHolder(IServiceProvider sp)
		{
			Impl = ActivatorUtilities.CreateInstance<T>(sp);
		}

		public T Impl { get; }
	}

	static void AssertNotNull(this IServiceCollection services)
	{
		if (services == null)
			throw new ArgumentNullException(nameof(services));
	}
	#endregion
}
