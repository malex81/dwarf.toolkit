namespace Dwarf.Toolkit.Basic.AsyncHelpers;

public class InvalidatorConfig
{
	public int Delay { get; set; } = 1;

	public static implicit operator InvalidatorConfig(int delay) => new() { Delay = delay };
	public static implicit operator InvalidatorConfig(TimeSpan time) => new() { Delay = (int)time.TotalMilliseconds };
}

public static class ActionFlow
{
	public static Action CreateInvalidator(Action callback, InvalidatorConfig? config = null)
	{
		Task? currentTask = null;
		var _config = config ?? new InvalidatorConfig();
		return () =>
		{
			lock (callback)
			{
				currentTask ??= Task.Run(async () =>
				{
					await Task.Delay(_config.Delay);
					lock (callback)
					{
						currentTask = null;
						callback();
					}
				});
			}
		};
	}

	public static Action CreateInvalidator(Func<Task> callback, InvalidatorConfig? config = null)
	{
		Task? currentTask = null;
		bool validState = true;
		var _config = config ?? new InvalidatorConfig();
		return () =>
		{
			lock (callback)
			{
				validState = false;
				currentTask ??= Task.Run(async () =>
				{
					while (!validState)
					{
						await Task.Delay(_config.Delay);
						validState = true;
						try { await callback(); }
						catch { }
						lock (callback)
						{
							if (validState)
							{
								currentTask = null;
								break;
							}
						}
					}
				});
			}
		};
	}

	public static Func<Task> Debounce(Action action, TimeSpan delay)
	{
		Task? currentTask = null;
		DateTime lastTime;
		return () =>
		{
			lock (action)
			{
				lastTime = DateTime.Now;
				currentTask ??= Task.Run(async () =>
				{
					while (DateTime.Now - lastTime < delay)
					{
						await Task.Delay(delay / 2);
					}
					lock (action)
					{
						currentTask = null;
						action();
					}
				});
				return currentTask;
			}
		};
	}

	public static Func<Action, Task> Debounce(TimeSpan delay)
	{
		object syncObject = new();
		Task? currentTask = null;
		DateTime lastTime;
		Action lastExec;
		return (exec) =>
		{
			lock (syncObject)
			{
				lastExec = exec;
				lastTime = DateTime.Now;
				currentTask ??= Task.Run(async () =>
				{
					while (DateTime.Now - lastTime < delay)
					{
						await Task.Delay(delay / 2);
					}
					lock (syncObject)
					{
						currentTask = null;
						lastExec();
					}
				});
				return currentTask;
			}
		};
	}

	public static Action<Action> Throttle(TimeSpan delay)
	{
		DateTime lastExec = DateTime.MinValue;
		return (exec) =>
		{
			DateTime now = DateTime.Now;
			if (now - lastExec < delay) return;
			lastExec = now;
			exec();
		};
	}
}
