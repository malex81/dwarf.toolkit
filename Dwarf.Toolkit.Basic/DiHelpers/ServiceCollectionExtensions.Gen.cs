using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

static partial class ServiceCollectionExtensions
{
	public static IServiceCollection AddSingleton<TS1, TS2, TImpl>(this IServiceCollection services)
		where TS1 : class
		where TS2 : class
		where TImpl : class, TS1, TS2
	{
		services.AssertNotNull();
		services.TryAddSingleton<ImplHolder<TImpl>>();
		services.AddSingleton<TS1>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS2>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		return services;
	}
	public static IServiceCollection AddSingleton<TS1, TS2, TS3, TImpl>(this IServiceCollection services)
		where TS1 : class
		where TS2 : class
		where TS3 : class
		where TImpl : class, TS1, TS2, TS3
	{
		services.AssertNotNull();
		services.TryAddSingleton<ImplHolder<TImpl>>();
		services.AddSingleton<TS1>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS2>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS3>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		return services;
	}
	public static IServiceCollection AddSingleton<TS1, TS2, TS3, TS4, TImpl>(this IServiceCollection services)
		where TS1 : class
		where TS2 : class
		where TS3 : class
		where TS4 : class
		where TImpl : class, TS1, TS2, TS3, TS4
	{
		services.AssertNotNull();
		services.TryAddSingleton<ImplHolder<TImpl>>();
		services.AddSingleton<TS1>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS2>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS3>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS4>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		return services;
	}
	public static IServiceCollection AddSingleton<TS1, TS2, TS3, TS4, TS5, TImpl>(this IServiceCollection services)
		where TS1 : class
		where TS2 : class
		where TS3 : class
		where TS4 : class
		where TS5 : class
		where TImpl : class, TS1, TS2, TS3, TS4, TS5
	{
		services.AssertNotNull();
		services.TryAddSingleton<ImplHolder<TImpl>>();
		services.AddSingleton<TS1>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS2>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS3>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS4>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		services.AddSingleton<TS5>(sp => sp.GetRequiredService<ImplHolder<TImpl>>().Impl);
		return services;
	}
}