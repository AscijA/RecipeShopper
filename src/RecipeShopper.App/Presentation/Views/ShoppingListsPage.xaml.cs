using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class ShoppingListsPage : ContentPage
{
    private ShoppingListsViewModel ViewModel => (ShoppingListsViewModel)BindingContext;

    public ShoppingListsPage(ShoppingListsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.OnAppearingAsync();
    }
}
