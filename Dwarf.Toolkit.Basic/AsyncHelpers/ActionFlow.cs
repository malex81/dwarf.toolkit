namespace Dwarf.Toolkit.Basic.AsyncHelpers;

public static partial class ActionFlow
{
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
						await Task.Delay((int)(delay.TotalMilliseconds / 2));
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
						await Task.Delay((int)(delay.TotalMilliseconds / 2));
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
