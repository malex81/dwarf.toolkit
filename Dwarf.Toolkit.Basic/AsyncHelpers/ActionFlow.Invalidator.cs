namespace Dwarf.Toolkit.Basic.AsyncHelpers;

public sealed class InvalidatorConfig
{
	public int Delay { get; set; } = 1;
	public bool ThrowExceptions { get; set; } = true;

	public static implicit operator InvalidatorConfig(int delay) => new() { Delay = delay };
	public static implicit operator InvalidatorConfig(TimeSpan time) => new() { Delay = (int)time.TotalMilliseconds };
}

internal sealed class InvalidatorContext
{
	CancellationTokenSource? tokenSource;

	public Task BuildTask(Func<CancellationToken, Task> body)
	{
		tokenSource = new();
		return Task.Run(() => body(tokenSource.Token), tokenSource.Token);
	}

	public void CancelLastTask() => tokenSource?.Cancel();
}

public class InvalidatorBase
{
	private readonly InvalidatorContext context;

	internal InvalidatorBase(InvalidatorContext context, InvalidatorConfig config, Func<Task> invalidate)
	{
		this.context = context;
		Config = config;
		Invalidate = invalidate;
	}
	public InvalidatorConfig Config { get; }
	public Func<Task> Invalidate { get; }

	public void CancelInvalidation() => context.CancelLastTask();

	public static implicit operator Func<Task>(InvalidatorBase invalidator) => invalidator.Invalidate;
	public static implicit operator Action(InvalidatorBase invalidator) => () => invalidator.Invalidate();
}

public sealed class Invalidator<T> : InvalidatorBase
{
	internal Invalidator(InvalidatorContext context, InvalidatorConfig config, T callback, Func<Task> invalidate)
		: base(context, config, invalidate)
	{
		Callback = callback;
	}
	public T Callback { get; }
}

static partial class ActionFlow
{
	public static Invalidator<Action> CreateInvalidator(Action callback, InvalidatorConfig? config = null)
	{
		Task? currentTask = null;
		var _context = new InvalidatorContext();
		var _config = config ?? new InvalidatorConfig();
		return new(_context, _config, callback, () =>
		{
			lock (callback)
			{
				if(currentTask != null && currentTask.IsCanceled)
					currentTask = null;
				currentTask ??= _context.BuildTask(async ct =>
				{
					await Task.Delay(_config.Delay, ct);
					lock (callback)
					{
						currentTask = null;
						if (ct.IsCancellationRequested)
							return;
						try { callback(); }
						catch
						{
							if (_config.ThrowExceptions)
								throw;
						}
					}
				});
				return currentTask;
			}
		});
	}

	public static Invalidator<Func<CancellationToken, Task>> CreateInvalidatorAsync(Func<CancellationToken, Task> callback, InvalidatorConfig? config = null)
	{
		Task? currentTask = null;
		bool validState = true;
		var _config = config ?? new InvalidatorConfig();
		var _context = new InvalidatorContext();
		return new(_context, _config, callback, () =>
		{
			lock (callback)
			{
				validState = false;
				if (currentTask != null && currentTask.IsCanceled)
					currentTask = null;
				currentTask ??= _context.BuildTask(async ct =>
				{
					while (!validState)
					{
						await Task.Delay(_config.Delay, ct);
						validState = true;
						if (!ct.IsCancellationRequested)
						{
							try { await callback(ct); }
							catch
							{
								if (_config.ThrowExceptions)
									throw;
							}
						}
						lock (callback)
						{
							if (validState || ct.IsCancellationRequested)
							{
								currentTask = null;
								break;
							}
						}
					}
				});
				return currentTask;
			}
		});
	}
}

public static class InvalidatorExtension
{
	public static void ForceCall(this InvalidatorBase invalidator)
	{
		if (invalidator is not Invalidator<Action> ia)
			throw new InvalidOperationException("In this context invalidator must be Invalidator<Action>");
		ia.ForceCall();
	}

	public static Task ForceCall(this InvalidatorBase invalidator, CancellationToken ct)
	{
		if (invalidator is not Invalidator<Func<CancellationToken, Task>> it)
			throw new InvalidOperationException("In this context invalidator must be Invalidator<Func<CancellationToken, Task>>");
		return it.ForceCall(ct);
	}

	public static void ForceCall(this Invalidator<Action> invalidator)
	{
		invalidator.CancelInvalidation();
		invalidator.Callback();
	}

	public static Task ForceCall(this Invalidator<Func<CancellationToken, Task>> invalidator, CancellationToken ct = default)
	{
		invalidator.CancelInvalidation();
		return invalidator.Callback(ct);
	}
}
