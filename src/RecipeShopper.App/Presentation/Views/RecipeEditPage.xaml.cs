using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class RecipeEditPage : ContentPage
{
    public RecipeEditPage(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
