using RecipeShopper.App.Presentation.Models;
using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class ShoppingPage : ContentPage
{
    private ShoppingViewModel ViewModel => (ShoppingViewModel)BindingContext;

    public ShoppingPage(ShoppingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.OnAppearingAsync();
    }

    private void OnItemCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox { BindingContext: ShoppingListItem item } && item.IsChecked != e.Value)
        {
            item.IsChecked = e.Value;
        }
        if (sender is CheckBox { BindingContext: ShoppingListItem changed })
        {
            ViewModel.SetChecked(changed, e.Value);
        }
    }
}
