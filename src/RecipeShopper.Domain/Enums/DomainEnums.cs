namespace RecipeShopper.Domain.Enums;

public enum MeasurementFamily
{
    Mass,
    Volume,
    Count,
    Container
}

public enum ShoppingListStatus
{
    Active,
    Completed
}

public enum ShoppingSourceType
{
    Recipe,
    MealPlanDay,
    MealPlanWeek,
    Manual
}

public enum ShoppingGroupingMode
{
    None,
    Recipe,
    Store
}

public enum AlternatingWeek
{
    A,
    B
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum CheckedItemBehavior
{
    MoveToBottom,
    Hide,
    KeepInPlace
}

public enum ImportConflictResolution
{
    KeepLocal,
    UseImported,
    KeepBoth
}

public enum ImportMode
{
    Merge,
    Replace
}

public enum PriceCoverageStatus
{
    Complete,
    Incomplete
}
