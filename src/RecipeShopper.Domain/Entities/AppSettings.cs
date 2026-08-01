using RecipeShopper.Domain.Common;
using RecipeShopper.Domain.Enums;

namespace RecipeShopper.Domain.Entities;

public sealed class AppSettings : Entity
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string AccentKey { get; set; } = "green";
    public ShoppingGroupingMode DefaultShoppingGrouping { get; set; } = ShoppingGroupingMode.None;
    public CheckedItemBehavior CheckedItemBehavior { get; set; } = CheckedItemBehavior.MoveToBottom;
    public bool ConfirmDestructiveActions { get; set; } = true;
    public DateOnly WeekAReferenceMonday { get; set; } = new(2026, 1, 5);
    public bool DailyReminderEnabled { get; set; }
    public TimeOnly DailyReminderTime { get; set; } = new(9, 0);
    public string CurrencyCode { get; set; } = "BAM";
}
