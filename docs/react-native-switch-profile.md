# React Native switch profile

The MAUI implementation is the active codebase. Do not maintain a second UI in parallel. If the project switches, preserve the domain behavior and the versioned JSON format and replace only the framework-specific projects.

## Baseline

- Expo SDK 57, React Native 0.86, strict TypeScript, Node 22.13+
- Expo Router with one root stack and four nested tab stacks
- Zustand for transient presentation state; SQLite remains authoritative
- React Hook Form and Zod for forms and validation
- `expo-sqlite` with numbered migrations, foreign keys, and WAL
- Expo image picker/manipulator/file-system, document picker, sharing, and local notifications
- Jest, React Native Testing Library, and Maestro
- EAS development builds and cloud production builds

## Compatibility contract

- Keep UUID identifiers, semantic icon keys, unit codes, quantity decimal strings, integer money values, and ISO currency codes.
- Preserve both JSON formats: simple catalogue import and full backup.
- Reuse the golden import/export fixtures and business-rule test cases from the .NET solution.
- Keep BCS copy, navigation, cart aggregation, Week A/B behavior, pricing rules, and archive behavior unchanged.

## Platform floors

- Android 7+
- iOS 16.4+

The switch is complete only when a full backup exported from the MAUI app imports into the React Native app without loss.
