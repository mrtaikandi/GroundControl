# Tower Design Language

This document defines the base visual language for The Tower frontend in [src/GroundControl.Tower](../src/GroundControl.Tower).

## Source Of Truth

- Design tokens live in [src/GroundControl.Tower/design-tokens/tokens.json](../src/GroundControl.Tower/design-tokens/tokens.json).
- Generated CSS variables are emitted to [src/GroundControl.Tower/src/styles/tokens.css](../src/GroundControl.Tower/src/styles/tokens.css).
- Shared frontend utilities live in [src/GroundControl.Tower/src/styles/tailwind.css](../src/GroundControl.Tower/src/styles/tailwind.css).
- Base primitives live in [src/GroundControl.Tower/src/components/ui](../src/GroundControl.Tower/src/components/ui).

Do not introduce new hard-coded shadows, scrims, radii, or recurring text sizes in feature components when a token or shared utility already exists.

## Surface Hierarchy

Use these layers consistently:

- `bg-page`: app canvas and sidebar background.
- `bg-container`: inset areas behind cards, previews, or grouped content.
- `bg-surface`: raised content surfaces such as cards and panels.
- `popover`: floating and modal surfaces.

Preferred shared utilities:

- `ui-surface-field`: inputs, selects, and textareas.
- `ui-surface-card`: standard raised content containers.
- `ui-surface-panel`: smaller nested panels within a larger surface.
- `ui-surface-floating`: popovers, menus, tooltips, and toasts.
- `ui-surface-modal`: dialogs and sheets.

## Radius Rules

- `radius-lg` (6px): controls and nested panels.
- `radius-xl` (8px): cards and content containers.
- `radius-2xl` (10px): floating surfaces and modals.
- `radius-pill` (9999px): non-icon buttons and segmented controls.

Icon-only controls can remain `rounded-lg` when a pill shape would look visually heavy.

## Elevation Rules

- Use token-backed button shadows for buttons and button-like active states.
- Use `ui-surface-floating` for floating elevation.
- Use `ui-surface-modal` for modal elevation.
- Use `ui-overlay-scrim` for dialog, sheet, and alert overlays.

Do not add raw `shadow-[...]` values in components unless the design language is being extended and the token source is updated first.

## Typography Rules

Preferred shared text utilities:

- `ui-text-body`: default application body text.
- `ui-text-body-sm`: compact body text.
- `ui-text-caption`: metadata, captions, helper text.
- `ui-text-section-title`: card and section headings.
- `ui-text-modal-title`: dialog and sheet headings.
- `ui-text-code`: inline machine-readable or JSON/code content.

Do not repeat raw values like `text-[13px]`, `text-[12.5px]`, `text-[11.5px]`, `text-[19px]`, or `text-[22px]` when one of the shared utilities is appropriate.

## Button Rules

- Standard buttons use pill corners.
- Icon buttons use `rounded-lg`.
- Primary and destructive hover/pressed feedback must use the interaction tokens, not raw brightness numbers.
- Prefer the shared `Button` primitive from [src/GroundControl.Tower/src/components/ui/button.tsx](../src/GroundControl.Tower/src/components/ui/button.tsx) over one-off button styling.

## Feature-Layer Guidance

- Build new screens from the `ui` primitives first.
- When a feature needs a new repeated visual treatment, add or extend a shared utility rather than duplicating classes across multiple feature components.
- If a style is specific to one feature and is unlikely to repeat, keep it local, but still consume existing color, radius, shadow, and type tokens.