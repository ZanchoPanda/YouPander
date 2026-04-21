using YouPander.ViewModels;

namespace YouPander.Views;

public partial class HistoryPage : ContentPage
{
	public HistoryPage()
	{
		InitializeComponent();
	}

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is HistoryViewModel vm)
            await vm.LoadAsync(); // <- hacer LoadAsync público
    }

}