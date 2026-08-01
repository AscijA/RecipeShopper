using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class IngredientDetailPage : ContentPage
{
    private IngredientDetailViewModel ViewModel => (IngredientDetailViewModel)BindingContext;

    public IngredientDetailPage(IngredientDetailViewModel viewModel)
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
