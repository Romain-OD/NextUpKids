using CommunityToolkit.Maui.Views;

namespace NextUpKids.Features.ParentAccess;

public partial class ParentGatePopup : Popup<bool>
{
	readonly ParentGateViewModel viewModel;
	bool closing;

	public ParentGatePopup(ParentGateViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		viewModel.Reset();
		BindingContext = viewModel;
	}

	async void OnSubmit(object? sender, EventArgs e)
	{
		if (closing) return;
		viewModel.SubmitCommand.Execute(null);
		if (viewModel.IsUnlocked)
		{
			closing = true;
			await CloseAsync(true);
		}
	}

	async void OnCancel(object? sender, EventArgs e)
	{
		if (closing) return;
		closing = true;
		await CloseAsync(false);
	}
}
