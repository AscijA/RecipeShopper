using RecipeShopper.App.Presentation.ViewModels;

namespace RecipeShopper.App.Presentation.Views;

public partial class MealPlannerPage : ContentPage
{
    public MealPlannerPage(MealPlannerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
