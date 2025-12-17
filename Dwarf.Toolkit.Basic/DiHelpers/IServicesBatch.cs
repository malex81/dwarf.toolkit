using Microsoft.Extensions.DependencyInjection;

namespace Dwarf.Toolkit.Basic.DiHelpers;

public interface IServicesBatch
{
	void Configure(IServiceCollection services);
}

public interface IServicesBatch<T>
{
	void Configure(IServiceCollection services, T prm);
}
