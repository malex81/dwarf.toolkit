using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Dwarf.Toolkit.Base.DiHelpers;

public enum BatchRepeatBehavior { Allow, Reject, Throw, Custom }

public sealed class ServicesBatchConfig
{
	#region static part
	private static readonly ServicesBatchConfig Default = new();
	private static readonly ConditionalWeakTable<IServiceCollection, ServicesBatchConfig> configDict = new();
	private static Func<IServiceCollection, ServicesBatchConfig>? provider;

	public static void UseConfigProvider(Func<IServiceCollection, ServicesBatchConfig> provider) => ServicesBatchConfig.provider = provider;
	public static ServicesBatchConfig GetConfig(IServiceCollection services) => configDict.GetValue(services, s => provider != null ? provider(s) : Default);
	#endregion

	private readonly HashSet<Type> addedBatches = [];
	private BatchRepeatBehavior repeatBehavior = BatchRepeatBehavior.Allow;

	public BatchRepeatBehavior RepeatBehavior
	{
		get => CustomRepeatHandler != null ? BatchRepeatBehavior.Custom
			: repeatBehavior == BatchRepeatBehavior.Custom ? BatchRepeatBehavior.Allow
			: repeatBehavior;
		set => repeatBehavior = value;
	}
	public Func<object, bool>? CustomRepeatHandler { get; set; }

	public bool AddBatchRequest(object batch)
	{
		if (RepeatBehavior == BatchRepeatBehavior.Allow)
			return true;
		var bType = batch.GetType();
		if (addedBatches.Contains(bType))
			return RepeatBehavior switch
			{
				BatchRepeatBehavior.Reject => false,
				BatchRepeatBehavior.Custom => CustomRepeatHandler!(batch),
				BatchRepeatBehavior.Throw => throw new Exception($"Batch {bType.FullName} already added"),
				_ => throw new Exception("Unexpected RepeatBehavior")
			};
		addedBatches.Add(bType);
		return true;
	}
}