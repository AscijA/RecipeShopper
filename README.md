# Moji Recepti

Offline-first recipe, meal-planning, ingredient catalogue, and shopping-list app for Android and iOS.

## Technology

- .NET 10 and .NET MAUI 10
- XAML and CommunityToolkit.Mvvm
- SQLite local persistence
- No backend, account, analytics, or required network connection

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

GitHub Actions can create signed Android APK/AAB and iOS IPA packages and attach them to a GitHub Release. Configure the required repository secrets using [the signing guide](docs/github-release-signing.md), then push a `v*` tag or run the release workflow manually.

## Product defaults

- BCS Latin UI
- BAM currency
- Warm off-white, green, and orange theme
- Recurring Week A/B meal plan
- Local JSON import/export and named shopping lists

The UI is backed by the normalized local repositories. Recipe, ingredient, shopping-list, planner, and settings changes are written to the app-private SQLite database. Import/export supports previewed merge or complete replacement, with per-conflict resolution.
