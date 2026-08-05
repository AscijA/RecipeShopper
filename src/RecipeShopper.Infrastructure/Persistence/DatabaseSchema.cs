using SQLite;

namespace RecipeShopper.Infrastructure.Persistence;

internal static class DatabaseSchema
{
    public const int CurrentVersion = 4;

    private static readonly string[] CreateStatements =
    [
        """
        CREATE TABLE recipe_categories (
            Id TEXT NOT NULL PRIMARY KEY,
            NameKey TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            IconKey TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE ingredients (
            Id TEXT NOT NULL PRIMARY KEY,
            NameKey TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            IconKey TEXT NULL,
            ImagePath TEXT NULL,
            MeasurementFamily TEXT NOT NULL,
            BaseUnit TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE recipes (
            Id TEXT NOT NULL PRIMARY KEY,
            CategoryId TEXT NOT NULL,
            Name TEXT NOT NULL,
            ImagePath TEXT NULL,
            Notes TEXT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            FOREIGN KEY (CategoryId) REFERENCES recipe_categories(Id) ON DELETE RESTRICT
        )
        """,
        """
        CREATE TABLE recipe_ingredients (
            Id TEXT NOT NULL PRIMARY KEY,
            RecipeId TEXT NOT NULL,
            IngredientId TEXT NOT NULL,
            Amount TEXT NULL,
            Unit TEXT NOT NULL,
            Note TEXT NULL,
            SortOrder INTEGER NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (RecipeId) REFERENCES recipes(Id) ON DELETE CASCADE,
            FOREIGN KEY (IngredientId) REFERENCES ingredients(Id) ON DELETE RESTRICT
        )
        """,
        """
        CREATE TABLE package_types (
            Id TEXT NOT NULL PRIMARY KEY,
            NameKey TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            UnitLabel TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE stores (
            Id TEXT NOT NULL PRIMARY KEY,
            NameKey TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            IsEnabled INTEGER NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE package_definitions (
            Id TEXT NOT NULL PRIMARY KEY,
            IngredientId TEXT NOT NULL,
            PackageTypeId TEXT NOT NULL,
            Label TEXT NULL,
            Amount TEXT NOT NULL,
            Unit TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            FOREIGN KEY (IngredientId) REFERENCES ingredients(Id) ON DELETE CASCADE,
            FOREIGN KEY (PackageTypeId) REFERENCES package_types(Id) ON DELETE RESTRICT
        )
        """,
        """
        CREATE TABLE store_offers (
            Id TEXT NOT NULL PRIMARY KEY,
            PackageDefinitionId TEXT NOT NULL,
            StoreId TEXT NOT NULL,
            PriceMinor INTEGER NOT NULL,
            PreviousPriceMinor INTEGER NULL,
            Currency TEXT NOT NULL,
            IsAvailable INTEGER NOT NULL,
            PriceUpdatedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (PackageDefinitionId) REFERENCES package_definitions(Id) ON DELETE CASCADE,
            FOREIGN KEY (StoreId) REFERENCES stores(Id) ON DELETE CASCADE,
            UNIQUE (PackageDefinitionId, StoreId, Currency)
        )
        """,
        """
        CREATE TABLE meal_slots (
            Id TEXT NOT NULL PRIMARY KEY,
            NameKey TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            ArchivedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE meal_plan_entries (
            Id TEXT NOT NULL PRIMARY KEY,
            WeekIndex INTEGER NOT NULL CHECK (WeekIndex IN (0, 1)),
            DayOfWeek INTEGER NOT NULL CHECK (DayOfWeek BETWEEN 0 AND 6),
            MealSlotId TEXT NOT NULL,
            RecipeId TEXT NOT NULL,
            Multiplier TEXT NOT NULL,
            SortOrder INTEGER NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (MealSlotId) REFERENCES meal_slots(Id) ON DELETE CASCADE,
            FOREIGN KEY (RecipeId) REFERENCES recipes(Id) ON DELETE CASCADE
        )
        """,
        """
        CREATE TABLE shopping_lists (
            Id TEXT NOT NULL PRIMARY KEY,
            Name TEXT NOT NULL,
            IsCompleted INTEGER NOT NULL,
            CompletedUtc TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL
        )
        """,
        """
        CREATE TABLE shopping_sources (
            Id TEXT NOT NULL PRIMARY KEY,
            ShoppingListId TEXT NOT NULL,
            Kind TEXT NOT NULL,
            RecipeId TEXT NULL,
            Label TEXT NULL,
            Multiplier TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (ShoppingListId) REFERENCES shopping_lists(Id) ON DELETE CASCADE
        )
        """,
        """
        CREATE TABLE shopping_contributions (
            Id TEXT NOT NULL PRIMARY KEY,
            ShoppingSourceId TEXT NOT NULL,
            IngredientId TEXT NOT NULL,
            Amount TEXT NULL,
            Unit TEXT NOT NULL,
            Note TEXT NULL,
            IngredientNameSnapshot TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (ShoppingSourceId) REFERENCES shopping_sources(Id) ON DELETE CASCADE,
            FOREIGN KEY (IngredientId) REFERENCES ingredients(Id) ON DELETE RESTRICT
        )
        """,
        """
        CREATE TABLE shopping_item_states (
            Id TEXT NOT NULL PRIMARY KEY,
            ShoppingListId TEXT NOT NULL,
            IngredientId TEXT NOT NULL,
            IsChecked INTEGER NOT NULL,
            SelectedStoreId TEXT NULL,
            Note TEXT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (ShoppingListId) REFERENCES shopping_lists(Id) ON DELETE CASCADE,
            FOREIGN KEY (IngredientId) REFERENCES ingredients(Id) ON DELETE RESTRICT,
            FOREIGN KEY (SelectedStoreId) REFERENCES stores(Id) ON DELETE SET NULL,
            UNIQUE (ShoppingListId, IngredientId)
        )
        """,
        """
        CREATE TABLE package_selections (
            Id TEXT NOT NULL PRIMARY KEY,
            ShoppingListItemId TEXT NOT NULL,
            PackageDefinitionId TEXT NOT NULL,
            StoreId TEXT NOT NULL,
            Quantity INTEGER NOT NULL CHECK (Quantity > 0),
            PriceMinorSnapshot INTEGER NOT NULL,
            CurrencySnapshot TEXT NOT NULL,
            NetAmountSnapshot TEXT NOT NULL,
            NetUnitSnapshot TEXT NOT NULL,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL,
            ArchivedUtc TEXT NULL,
            FOREIGN KEY (ShoppingListItemId) REFERENCES shopping_item_states(Id) ON DELETE CASCADE,
            FOREIGN KEY (PackageDefinitionId) REFERENCES package_definitions(Id) ON DELETE RESTRICT,
            FOREIGN KEY (StoreId) REFERENCES stores(Id) ON DELETE RESTRICT,
            UNIQUE (ShoppingListItemId, PackageDefinitionId, StoreId)
        )
        """,
        """
        CREATE TABLE app_settings (
            Key TEXT NOT NULL PRIMARY KEY,
            JsonValue TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        )
        """,
        "CREATE INDEX idx_recipes_category ON recipes(CategoryId)",
        "CREATE INDEX idx_recipe_ingredients_recipe ON recipe_ingredients(RecipeId, SortOrder)",
        "CREATE INDEX idx_package_definitions_ingredient ON package_definitions(IngredientId, SortOrder)",
        "CREATE INDEX idx_store_offers_package ON store_offers(PackageDefinitionId)",
        "CREATE INDEX idx_meal_plan_position ON meal_plan_entries(WeekIndex, DayOfWeek, MealSlotId, SortOrder)",
        "CREATE INDEX idx_shopping_sources_list ON shopping_sources(ShoppingListId)",
        "CREATE INDEX idx_shopping_contributions_source ON shopping_contributions(ShoppingSourceId)",
        "CREATE INDEX idx_shopping_contributions_ingredient ON shopping_contributions(IngredientId)",
        "CREATE INDEX idx_package_selections_item ON package_selections(ShoppingListItemId)"
    ];

    public static void Migrate(SQLiteConnection connection, int fromVersion)
    {
        if (fromVersion < 0 || fromVersion > CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported database schema version {fromVersion}.");
        }

        if (fromVersion < 1)
        {
            foreach (var statement in CreateStatements)
            {
                connection.Execute(statement);
            }

            Seed(connection);
            connection.Execute("PRAGMA user_version = 1");
        }

        if (fromVersion < 2)
        {
            connection.Execute(
                """
                CREATE TABLE sync_workspace (
                    Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                    WorkspaceId TEXT NOT NULL,
                    ShareCode TEXT NOT NULL,
                    DeviceId TEXT NOT NULL,
                    Revision INTEGER NOT NULL,
                    LastSyncedUtc TEXT NULL
                )
                """);
            connection.Execute(
                """
                CREATE TABLE sync_outbox (
                    Id TEXT NOT NULL PRIMARY KEY,
                    CreatedUtc TEXT NOT NULL
                )
                """);
            connection.Execute("PRAGMA user_version = 2");
        }

        if (fromVersion < 3)
        {
            if (fromVersion >= 1)
            {
                connection.Execute("ALTER TABLE ingredients ADD COLUMN ImagePath TEXT NULL");
            }
            connection.Execute("PRAGMA user_version = 3");
        }

        if (fromVersion < 4)
        {
            const string timestamp = "2000-01-01T00:00:00.0000000+00:00";
            connection.Execute(
                "INSERT OR IGNORE INTO recipe_categories (Id, NameKey, Name, IconKey, SortOrder, ArchivedUtc, CreatedUtc, UpdatedUtc) VALUES (?, ?, ?, ?, ?, NULL, ?, ?)",
                "00000000-0000-0000-0001-000000000011", TextNormalization.NameKey("Ručak"), "Ručak", "category-lunch", 9, timestamp, timestamp);
            connection.Execute(
                "INSERT OR IGNORE INTO recipe_categories (Id, NameKey, Name, IconKey, SortOrder, ArchivedUtc, CreatedUtc, UpdatedUtc) VALUES (?, ?, ?, ?, ?, NULL, ?, ?)",
                "00000000-0000-0000-0001-000000000012", TextNormalization.NameKey("Večera"), "Večera", "category-dinner", 10, timestamp, timestamp);
            connection.Execute("UPDATE recipe_categories SET SortOrder = 11 WHERE Id = ?", "00000000-0000-0000-0001-000000000010");
            connection.Execute("PRAGMA user_version = 4");
        }
    }

    private static void Seed(SQLiteConnection connection)
    {
        const string timestamp = "2000-01-01T00:00:00.0000000+00:00";
        var categories = new (string Id, string Name, string Icon)[]
        {
            ("00000000-0000-0000-0001-000000000001", "Govedina", "category-beef"),
            ("00000000-0000-0000-0001-000000000002", "Piletina", "category-chicken"),
            ("00000000-0000-0000-0001-000000000003", "Riba", "category-fish"),
            ("00000000-0000-0000-0001-000000000004", "Supa/Čorba", "category-soup"),
            ("00000000-0000-0000-0001-000000000005", "Tijesto", "category-dough"),
            ("00000000-0000-0000-0001-000000000006", "Slatko", "category-sweet"),
            ("00000000-0000-0000-0001-000000000007", "Vegetarijansko", "category-vegetarian"),
            ("00000000-0000-0000-0001-000000000008", "Salata", "category-salad"),
            ("00000000-0000-0000-0001-000000000009", "Doručak", "category-breakfast"),
            ("00000000-0000-0000-0001-000000000011", "Ručak", "category-lunch"),
            ("00000000-0000-0000-0001-000000000012", "Večera", "category-dinner"),
            ("00000000-0000-0000-0001-000000000010", "Ostalo", "category-other")
        };

        for (var index = 0; index < categories.Length; index++)
        {
            var item = categories[index];
            connection.Insert(new RecipeCategoryRow
            {
                Id = item.Id,
                Name = item.Name,
                NameKey = TextNormalization.NameKey(item.Name),
                IconKey = item.Icon,
                SortOrder = index,
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp
            });
        }

        var packageTypes = new[] { "Pakovanje", "Kesica", "Kutija", "Boca", "Tegla", "Konzerva" };
        for (var index = 0; index < packageTypes.Length; index++)
        {
            connection.Insert(new PackageTypeRow
            {
                Id = $"00000000-0000-0000-0002-{index + 1:000000000000}",
                Name = packageTypes[index],
                NameKey = TextNormalization.NameKey(packageTypes[index]),
                UnitLabel = packageTypes[index].ToLowerInvariant(),
                SortOrder = index,
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp
            });
        }

        var mealSlots = new[] { "Doručak", "Ručak", "Večera", "Ostalo" };
        for (var index = 0; index < mealSlots.Length; index++)
        {
            connection.Insert(new MealSlotRow
            {
                Id = $"00000000-0000-0000-0003-{index + 1:000000000000}",
                Name = mealSlots[index],
                NameKey = TextNormalization.NameKey(mealSlots[index]),
                SortOrder = index,
                CreatedUtc = timestamp,
                UpdatedUtc = timestamp
            });
        }

        InsertSetting(
            connection,
            "app-settings",
            "{\"id\":\"00000000-0000-0000-0004-000000000001\",\"createdAtUtc\":\"2000-01-01T00:00:00+00:00\",\"updatedAtUtc\":\"2000-01-01T00:00:00+00:00\",\"archivedAtUtc\":null,\"theme\":\"System\",\"accentKey\":\"green\",\"defaultShoppingGrouping\":\"None\",\"checkedItemBehavior\":\"MoveToBottom\",\"confirmDestructiveActions\":true,\"weekAReferenceMonday\":\"2026-01-05\",\"dailyReminderEnabled\":false,\"dailyReminderTime\":\"09:00:00\",\"currencyCode\":\"BAM\"}",
            timestamp);
    }

    private static void InsertSetting(SQLiteConnection connection, string key, string json, string timestamp) =>
        connection.Insert(new AppSettingRow { Key = key, JsonValue = json, UpdatedUtc = timestamp });
}
