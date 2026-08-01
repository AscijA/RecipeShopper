using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class IngredientsPage : ContentPage
{
    private IngredientsViewModel ViewModel => (IngredientsViewModel)BindingContext;

    public IngredientsPage(IngredientsViewModel viewModel)
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
