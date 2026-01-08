using Dwarf.Toolkit.Basic.AsyncHelpers;
using NUnit.Framework;
using System.Diagnostics;

namespace Dwarf.Toolkit.Basic.Tests;

[TestFixture]
internal sealed class ActionFlowTest
{
	[TestCase(8)]
	[TestCase(12)]
	[TestCase(24)]
	public async Task Throttle(int delay)
	{
		var throttle = ActionFlow.Throttle(TimeSpan.FromMilliseconds(100));
		var res = new List<int>();
		for (int i = 0; i < 100; i++)
		{
			throttle(() =>
			{
				res.Add(i);
			});
			await Task.Delay(delay);
		}
		Console.WriteLine(string.Join(',', res));
		Assert.That(res.Count, Is.InRange(delay, 2 * delay));
	}

	[TestCase(20)]
	[TestCase(60)]
	[TestCase(140)]
	public async Task Debounce(int delay)
	{
		int DDeleay = 80;
		int COUNT = 20;
		int num = 0;
		var res = new List<int>();
		var debounce = ActionFlow.Debounce(() => res.Add(num), TimeSpan.FromMilliseconds(DDeleay));
		Task lastTask = Task.CompletedTask;
		for (int i = 0; i < COUNT; i++)
		{
			num = i;
			lastTask = debounce();
			await Task.Delay(delay);
		}
		debounce();
		await lastTask;
		Console.WriteLine(string.Join(',', res));
		if (delay < DDeleay)
		{
			Assert.That(res.Count, Is.EqualTo(1));
			Assert.That(res[0], Is.EqualTo(COUNT - 1));
		}
		else
			Assert.That(res.Count, Is.EqualTo(COUNT));
	}

	[TestCase(20)]
	[TestCase(60)]
	[TestCase(140)]
	public async Task Debounce2(int delay)
	{
		int DDeleay = 80;
		int COUNT = 20;
		var debounce = ActionFlow.Debounce(TimeSpan.FromMilliseconds(DDeleay));
		var res = new List<int>();
		Task lastTask = Task.CompletedTask;
		for (int i = 0; i < COUNT; i++)
		{
			var num = i;
			lastTask = debounce(() =>
			{
				res.Add(num);
			});
			await Task.Delay(delay);
		}
		await lastTask;
		Console.WriteLine(string.Join(',', res));
		if (delay < DDeleay)
		{
			Assert.That(res.Count, Is.EqualTo(1));
			Assert.That(res[0], Is.EqualTo(COUNT - 1));
		}
		else
			Assert.That(res.Count, Is.EqualTo(COUNT));
	}

	[TestCase(10)]
	[TestCase(40)]
	[TestCase(100)]
	public async Task Invalidator(int delay)
	{
		int DDeleay = 65;
		int COUNT = 20;
		int num = 0;
		var res = new List<int>();
		var invalid = ActionFlow.CreateInvalidator(() => res.Add(num), TimeSpan.FromMilliseconds(DDeleay));
		Task lastTask = Task.CompletedTask;
		var sw = Stopwatch.StartNew();
		for (int i = 0; i < COUNT; i++)
		{
			num = i;
			lastTask = invalid();
			await Task.Delay(delay);
		}
		sw.Stop();
		await lastTask;
		Console.WriteLine(string.Join(',', res));
		if (delay < DDeleay)
		{
			var _delay = sw.ElapsedMilliseconds / COUNT;
			Assert.That(res.Count, Is.EqualTo(COUNT / (DDeleay / _delay + 1)));
		}
		else
			Assert.That(res.Count, Is.EqualTo(COUNT));
		Assert.That(res.Last(), Is.EqualTo(COUNT - 1));
	}

	[TestCase(10)]
	[TestCase(40)]
	[TestCase(120)]
	public async Task InvalidatorAsync(int delay)
	{
		int DDeleay = 50;
		int COUNT = 20;
		int num = 0;
		var res = new List<int>();
		var invalid = ActionFlow.CreateInvalidatorAsync(async () =>
		{
			res.Add(num);
			await Task.Delay(DDeleay);
			throw new Exception("This error should be caught");
		}, new() { Delay = DDeleay, ThrowExceptions = false });
		Task lastTask = Task.CompletedTask;
		var sw = Stopwatch.StartNew();
		for (int i = 0; i < COUNT; i++)
		{
			num = i;
			lastTask = invalid();
			await Task.Delay(delay);
		}
		sw.Stop();
		await lastTask;
		Console.WriteLine(string.Join(',', res));
		var dd2 = 2 * DDeleay;
		if (delay < dd2)
		{
			var _delay = sw.ElapsedMilliseconds / COUNT;
			Assert.That(res.Count, Is.InRange(COUNT / (dd2 / _delay + 1), COUNT / (DDeleay / _delay + 1)));
		}
		else
			Assert.That(res.Count, Is.EqualTo(COUNT));
		Assert.That(res.Last(), Is.EqualTo(COUNT - 1));
	}

	[TestCase(20)]
	[TestCase(40)]
	public async Task InvalidatorWithException(int delay)
	{
		int DDeleay = 65;
		int COUNT = 20;
		int num = 0;
		var res = new List<int>();
		var invalid = ActionFlow.CreateInvalidator(() =>
		{
			if (num > 10)
				throw new InvalidOperationException("Num must be less than 10");
			res.Add(num);
		}, TimeSpan.FromMilliseconds(DDeleay));
		Task lastTask = Task.CompletedTask;
		var sw = Stopwatch.StartNew();
		for (int i = 0; i < COUNT; i++)
		{
			num = i;
			lastTask = invalid();
			await Task.Delay(delay);
		}
		sw.Stop();
		Assert.That(async () => await lastTask, Throws.Exception);
		Console.WriteLine(string.Join(',', res));		
	}

	[TestCase(20)]
	[TestCase(40)]
	public async Task InvalidatorWithExceptionAsync(int delay)
	{
		int DDeleay = 65;
		int COUNT = 20;
		int num = 0;
		var res = new List<int>();
		var invalid = ActionFlow.CreateInvalidatorAsync(async () =>
		{
			await Task.Delay(10);
			if (num > 10)
				throw new InvalidOperationException("Num must be less than 10");
			res.Add(num);
		}, TimeSpan.FromMilliseconds(DDeleay));
		Task lastTask = Task.CompletedTask;
		var sw = Stopwatch.StartNew();
		for (int i = 0; i < COUNT; i++)
		{
			num = i;
			lastTask = invalid();
			await Task.Delay(delay);
		}
		sw.Stop();
		Assert.That(async () => await lastTask, Throws.Exception);
		Console.WriteLine(string.Join(',', res));
	}
}