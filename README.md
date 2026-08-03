# Moji Recepti

Offline-first recipe, meal-planning, ingredient catalogue, and shopping-list app for Android and iOS.

## Technology

- .NET 10 and .NET MAUI 10
- XAML and CommunityToolkit.Mvvm
- SQLite local persistence
- SQLite-first offline operation with optional Supabase workspace synchronization

## Projects

- `RecipeShopper.Domain` — entities and business rules
- `RecipeShopper.Application` — use-case and repository contracts
- `RecipeShopper.Infrastructure` — SQLite and JSON import/export
- `RecipeShopper.App` — MAUI UI and platform adapters
- `RecipeShopper.Domain.Tests` and `RecipeShopper.Infrastructure.Tests` — automated tests

## Build

```powershell
dotnet restore RecipeShopper.slnx
dotnet test tests/RecipeShopper.Domain.Tests/RecipeShopper.Domain.Tests.csproj
dotnet test tests/RecipeShopper.Infrastructure.Tests/RecipeShopper.Infrastructure.Tests.csproj
dotnet build src/RecipeShopper.App/RecipeShopper.App.csproj -f net10.0-android
```

iOS compilation and signing require a macOS runner with compatible Xcode and signing credentials.

## Releases

GitHub Actions creates signed Android APK/AAB packages and an unsigned iOS device IPA for Sideloadly, then attaches them to a GitHub Release. Configure the Android repository secrets using [the signing guide](docs/github-release-signing.md), then push a `v*` tag or run the release workflow manually.

## Product defaults

- BCS Latin UI
- BAM currency
- Warm off-white, green, and orange theme
- Recurring Week A/B meal plan
- Local JSON import/export and named shopping lists

The UI is backed by the normalized local repositories. Recipe, ingredient, shopping-list, planner, and settings changes are written to the app-private SQLite database. Import/export supports previewed merge or complete replacement, with per-conflict resolution.

## Optional Supabase sync

Run [`docs/supabase-sync.sql`](docs/supabase-sync.sql) in a Supabase project, then provide these values when launching/building the app:

```powershell
dotnet build src/RecipeShopper.App/RecipeShopper.App.csproj -f net10.0-android `
  -p:RecipeShopperSupabaseUrl='https://PROJECT.supabase.co' `
  -p:RecipeShopperSupabaseAnonKey='YOUR_PUBLIC_ANON_KEY'
```

Without these values the app remains completely local and the shared-space controls explain that sync is not configured. Local writes are always committed to SQLite first and recorded in a durable outbox; activation or the **Sinhronizuj** action pushes them when connectivity returns.
