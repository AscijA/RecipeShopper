using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Domain.Entities;

public sealed class MealSlot : Entity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class MealPlanEntry : Entity
{
    public AlternatingWeek Week { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public Guid MealSlotId { get; set; }
    public Guid RecipeId { get; set; }
    public decimal RecipeMultiplier { get; set; } = 1m;
    public int SortOrder { get; set; }
}
