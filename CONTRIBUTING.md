# Contributing

Thanks for checking out DDNetNW.

## Before contributing

- Keep the app local-first.
- Do not add private tokens, webhooks or credentials.
- Do not hardcode personal watched nicknames.
- Keep UI and data logic separated where possible.

## Suggested workflow

1. Open an issue or describe the change.
2. Keep the change focused.
3. Update documentation if the behavior changes.
4. Test the app on Windows before release.

## Code style

- C# with nullable reference types enabled.
- Prefer small services for data logic.
- Avoid putting network parsing directly into WPF event handlers.
- Keep UI text localizable.

## Current design rule

Quick Search logic belongs in `Services/NicknameSearchService.cs`, not directly in `MainWindow.xaml.cs`.
