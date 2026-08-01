using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class IngredientEditPage : ContentPage
{
    public IngredientEditPage(IngredientEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
