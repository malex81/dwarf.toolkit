using Dwarf.Toolkit.Demo.Bunnies;

namespace Dwarf.Toolkit.Demo;

public partial class MainPage : ContentPage
{
	readonly ExampleBindableObject sample = new();

	public MainPage()
	{
		InitializeComponent();
	}

	private void OnCounterClicked(object sender, EventArgs e)
	{
		textLabel.Text = $"{sample.TextProp} - {sample.CustomProp.Text} ({sample.CustomProp.Num})";

		sample.NumProp++;
		var count = sample.NumProp;

		if (count == 1)
			CounterBtn.Text = $"Clicked {count} time";
		else
			CounterBtn.Text = $"Clicked {count} times";

		SemanticScreenReader.Announce(CounterBtn.Text);
	}
}

