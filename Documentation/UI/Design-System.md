# Pillar Design System

Pillar uses the native WPF Fluent theme as its control foundation and adds a small product-specific design system. The goal is to make visual experimentation inexpensive without coupling the application to a third-party UI framework.

## Resource Structure

- `src/Pillar.UI/Themes/Pillar.Palette.xaml` owns application-wide colors and semantic brushes.
- `src/Pillar.UI/Themes/Pillar.Typography.xaml` owns shared typography metrics and text roles.
- `src/Pillar.UI/Themes/Controls` owns shared control metrics and one resource dictionary per control target type.
- `src/Pillar.UI/App.xaml` merges those control dictionaries directly in dependency order so WPF can resolve cross-dictionary `StaticResource` references.
- Individual views own brushes and styles used only by that view.
- `src/Pillar.UI/App.xaml` selects the native WPF theme mode and merges the Pillar dictionaries.

Keep raw color values out of individual views. A view-specific brush should reference a palette color such as `Pillar.Color.Background2`, `Pillar.Color.Border`, or `Pillar.Color.Accent`.

Overlay panels define their own background, border, and component brushes in their `UserControl.Resources`. Those brushes use dynamic references to the shared palette colors. Changing a palette color updates every view that uses it, while changing one local brush lets that view diverge without adding view knowledge to the global theme.

## Visual Direction

The initial theme is a compact dark CAD workspace:

- neutral charcoal surfaces keep attention on the model
- raised overlay panels separate tools from the viewport
- teal is the primary selection and workflow accent
- warning and danger colors are reserved for status meaning
- spacing follows a compact four-pixel rhythm

## Naming Rules

- `Pillar.Color.*` resources are raw palette values.
- `Pillar.Brush.*` resources express application-wide UI roles.
- View-specific brush names describe the owning component, such as `ModePanelBackgroundBrush`.
- `Pillar.*Style` resources are reusable components or typography roles.
- rendering colors remain in the rendering layer unless they represent WPF-only viewport chrome.

## Changing The Theme

For a color experiment, edit the core palette values in `Pillar.Palette.xaml`. To override one panel, change its local `SolidColorBrush` to use a different palette token. Dynamic resource references allow XAML Hot Reload and a future light/dark dictionary switch to update existing controls.

When a new recurring visual pattern appears in three or more views, promote it into `Pillar.Controls.xaml`. Keep one-off layout and workflow visibility rules local to the owning view.

## Compatibility Resources

The shared `ModeOverlay*` typography resources remain application-level compatibility names because they are consumed by many tool panels. Component-specific resources, including clip-slider and Mode Panel tab brushes, stay with their owning controls.
