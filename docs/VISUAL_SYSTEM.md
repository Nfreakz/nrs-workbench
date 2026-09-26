# NRS Workbench visual system

## Intent

NRS Workbench is a Windows developer utility, not a gaming dashboard and not an AI-styled product surface. The interface should feel restrained, dense enough for technical work, and comfortable beside IDEs, terminals and browser developer tools.

## Core palette

- Window background: `#101216`
- Primary surface: `#171A1F`
- Secondary surface: `#1D2128`
- Elevated surface: `#242931`
- Border: `#303640`
- Primary text: `#F1F3F5`
- Muted text: `#9BA3AE`
- Brand/accent: `#5B82C7`
- Soft accent: `#28364B`
- Success: `#5AA878`
- Warning: `#C59A4A`
- Error/stopped: `#D2636D`

Blue is an accent, not the canvas. New components should not introduce additional blue surface families when an existing neutral surface or accent token is sufficient.

## Typography

- UI family: Segoe UI.
- Technical/log/diff text: Cascadia Mono, with Consolas fallback.
- Do not use script, handwriting or decorative fonts for the Neo RS mark.
- Normal body text: 12–13 px.
- Metadata and compact labels: 10.5–11 px minimum.
- Section titles: 14–16 px.
- Window/page titles: approximately 24–27 px.
- Use font weight and spacing before adding color to create hierarchy.

## Components

- Prefer neutral cards and borders. Status color belongs in a value, dot, badge or small accent rather than around an entire KPI card.
- Default card radius: 8 px. Controls should generally use 6 px.
- Avoid nested card-inside-card layouts unless the nested element communicates a real state or interaction boundary.
- Buttons use a neutral graphite background. Primary actions may use the restrained blue accent.
- Disabled state should rely on opacity, not a new color family.

## Status color

Use green, amber and red only when they communicate runner/repository state, warnings or errors. Do not use status colors as decoration.

Repository and runner state badges may use a subtle tinted background, but the tint must remain darker and less saturated than the text/status accent.

## Density

NRS Workbench is information-dense, but information should remain readable at normal Windows scaling. Avoid labels below 10.5 px. Prefer fewer visible separators and larger spacing groups over many small bordered boxes.

## Branding

The Neo RS name may appear discreetly in footers/About using normal Segoe UI and muted text. Avoid signature/script styling, neon glow, gradients or oversized brand marks inside working views.

## Rule for future changes

Before adding a new hard-coded color to XAML, check whether the existing theme token or a current status color already covers the need. If a genuinely new semantic color is required, add it to the shared theme rather than inventing a one-off per-window shade.
