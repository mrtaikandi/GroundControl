---
version: alpha
name: GroundControl — The Tower
description: >
  Design system for the GroundControl admin surface (The Tower). Reserved,
  editorial, and calm — built for engineers who need to trust what they're
  looking at. Every visual decision prioritises legibility, hierarchy, and
  restraint over decoration.
colors:
  primary: "#6e4ad8"
  primary-hover: "#5a39bc"
  primary-text: "#452a92"
  primary-50: "#f4f1fb"
  primary-100: "#e7e1f6"
  primary-200: "#cdc1ee"
  neutral-white: "#ffffff"
  neutral-50: "#fcfcfc"
  neutral-100: "#f4f4f6"
  neutral-200: "#e7e6eb"
  neutral-300: "#d1d0d8"
  neutral-400: "#a5a3b0"
  neutral-500: "#74727f"
  neutral-600: "#55535e"
  neutral-700: "#3c3a44"
  neutral-800: "#27262d"
  neutral-900: "#16151a"
  neutral-950: "#0a090c"
  success: "#4fb848"
  success-50: "#eaf3e8"
  success-800: "#1f561d"
  warning: "#c29523"
  warning-50: "#fbf5e4"
  warning-800: "#5a430a"
  error: "#c9483a"
  error-50: "#fbe9e6"
  error-700: "#a53929"
  surface: "#ffffff"
  on-surface: "#16151a"
typography:
  headline-display:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 34px
    fontWeight: 700
    lineHeight: 1.1
    letterSpacing: -0.025em
  headline-lg:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 22px
    fontWeight: 500
    lineHeight: 1.2
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 19px
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: -0.015em
  body-lg:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 14.5px
    fontWeight: 400
    lineHeight: 1.5
  body-md:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.5
  body-sm:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 12.5px
    fontWeight: 400
    lineHeight: 1.5
  label-md:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 11.5px
    fontWeight: 400
    lineHeight: 1.4
  label-sm:
    fontFamily: Inter, Segoe UI Variable, Segoe UI, -apple-system, system-ui, sans-serif
    fontSize: 11px
    fontWeight: 500
    lineHeight: 1.4
    letterSpacing: 0.08em
  code:
    fontFamily: Cascadia Code, Cascadia Mono, Consolas, SF Mono, ui-monospace, Menlo, monospace
    fontSize: 12.5px
    fontWeight: 400
    lineHeight: 1.5
rounded:
  none: 0px
  sm: 3px
  md: 4px
  lg: 6px
  xl: 8px
  2xl: 10px
  full: 9999px
spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 20px
  2xl: 24px
  3xl: 32px
  page-v: 36px
  page-h: 56px
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.full}"
    padding: 10px 16px
    typography: "{typography.body-md}"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.neutral-700}"
    rounded: "{rounded.full}"
    padding: 10px 16px
    typography: "{typography.body-md}"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.neutral-500}"
    rounded: "{rounded.lg}"
    padding: 6px 10px
    typography: "{typography.body-sm}"
  chip-default:
    backgroundColor: "{colors.neutral-100}"
    textColor: "{colors.neutral-700}"
    rounded: "{rounded.md}"
    padding: 2px 6px
    typography: "{typography.code}"
  chip-selected:
    backgroundColor: "{colors.neutral-900}"
    textColor: "{colors.surface}"
    rounded: "{rounded.md}"
    padding: 2px 6px
    typography: "{typography.code}"
  badge-neutral:
    backgroundColor: "{colors.neutral-100}"
    textColor: "{colors.neutral-700}"
    rounded: "{rounded.md}"
    padding: 2px 8px
    typography: "{typography.label-md}"
  badge-info:
    backgroundColor: "{colors.primary-50}"
    textColor: "{colors.primary-text}"
    rounded: "{rounded.md}"
    padding: 2px 8px
    typography: "{typography.label-md}"
  badge-success:
    backgroundColor: "{colors.success-50}"
    textColor: "{colors.success-800}"
    rounded: "{rounded.md}"
    padding: 2px 8px
    typography: "{typography.label-md}"
  badge-warning:
    backgroundColor: "{colors.warning-50}"
    textColor: "{colors.warning-800}"
    rounded: "{rounded.md}"
    padding: 2px 8px
    typography: "{typography.label-md}"
  badge-critical:
    backgroundColor: "{colors.error-50}"
    textColor: "{colors.error-700}"
    rounded: "{rounded.md}"
    padding: 2px 8px
    typography: "{typography.label-md}"
  input-field:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.neutral-700}"
    rounded: "{rounded.lg}"
    padding: 8px 12px
    typography: "{typography.body-md}"
  segmented-control:
    backgroundColor: "{colors.neutral-100}"
    textColor: "{colors.neutral-500}"
    rounded: "{rounded.full}"
    padding: 3px
    typography: "{typography.body-sm}"
  segmented-control-active:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.neutral-900}"
    rounded: "{rounded.full}"
    typography: "{typography.body-sm}"
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.xl}"
    padding: 24px
  nav-item:
    backgroundColor: "transparent"
    textColor: "{colors.neutral-600}"
    rounded: "{rounded.lg}"
    padding: 8px 12px
    typography: "{typography.body-md}"
  nav-item-active:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.neutral-900}"
    rounded: "{rounded.lg}"
    padding: 8px 12px
    typography: "{typography.body-md}"
---

# GroundControl — The Tower

## Overview

The Tower is the admin surface for GroundControl, a centralised configuration
service. Its visual identity is **reserved, editorial, and calm** — built for
engineers who visit this surface when they need to *trust* what they are looking
at. Every design decision prioritises legibility, hierarchy, and restraint.

The product manages config entries, scoped values, snapshots, and client
credentials. Engineers read and edit structured, machine-shaped data; that
reality drives the aesthetics. Prose is kept to a minimum; identifiers —
project IDs, entry keys, scope dimensions, API endpoints — are the primary
language of the UI.

**Personality:** Institutional, precise, and unhurried. No gradients, no
illustrations, no decorative flourishes. The product borrows from editorial
print design rather than consumer apps.

**Target audience:** Platform and infrastructure engineers. They value density,
speed, and predictability. The default density is Comfortable; a Compact density
tightens vertical rhythm across the surface without changing type sizes.

**Emotional register:** Trust and authority. When an engineer looks at a config
value, a snapshot history, or an audit record, they should feel certain they are
seeing the truth — no ambiguity, no guesswork.

## Colors

The palette is built from a desaturated neutral ramp anchored by a single
violet accent. Status is the only place colour carries meaning beyond
decoration.

- **Violet / Indigo (primary, `#6e4ad8`):** The sole brand expression.
  Reserved exclusively for selected states, primary action buttons, active
  navigation rails, and focus rings. It never decorates; it signals
  *"this is active, selected, or the most important action on this screen."*
- **Neutral ramp (`#fcfcfc` → `#0a090c`):** A desaturated cool-grey ramp
  carries all structure — page backgrounds, dividers, surface layers, body
  text, and headings. The 50-step is the page background; white (`#ffffff`)
  raises cards and inputs one level.
- **Success green (`#4fb848`):** Live indicators, active pills, and OK status.
- **Warning amber (`#c29523`):** Advisory conditions and the Publisher role
  badge.
- **Critical red (`#c9483a`):** Destructive actions, revoked tokens, and the
  Admin role badge.
- **Semantic surface:** Components always consume the semantic role tokens
  (`bg.page`, `bg.surface`, `bg.selected`, `bg.chipSelected`, `fg.heading`,
  `fg.body`, `fg.caption`, etc.) — never the raw palette. This guarantees
  that both light and dark modes apply the correct contrast pair from a single
  token reference.

### Dark mode

Every raw palette value ships with a light and a dark variant. In dark mode
the neutral ramp inverts — near-black surfaces gain near-white text — and the
violet accent shifts to `#8e6eff` for adequate contrast on dark backgrounds.
Selected and chip-selected states use translucent violet fills so layering
reads correctly regardless of what surface sits beneath them. The syntax
palette rebalances completely (VSCode-inspired dark tokens) so code blocks
remain legible on `#333337` container backgrounds.

## Typography

The typography strategy relies on two families: **Inter** for all prose, UI
labels, and page structure; **Cascadia Code** for every machine-shaped
identifier.

- **Display / page title (`34px / 700 / −0.025em`):** The editorial anchor.
  Every screen opens with a large, tight-tracked headline that orients the
  engineer in under a second.
- **Section title (`19px / 700 / −0.015em`):** Table section headers, card
  titles, modal subheadings.
- **Modal title (`22px / 500 / −0.015em`):** The publish modal and confirm
  dialogs.
- **Body large (`14.5px / 400`):** Table primary cells and panel body copy.
- **Body (`13px / 400`):** Default body text — the workhorse size for labels,
  descriptions, and list rows.
- **Body small (`12.5px / 400`):** Captions below primary cells, sub-lines in
  list rows.
- **Label / caption (`11.5px / 400`):** Timestamps, metadata lines, field
  labels.
- **Eyebrow (`11px / 500 / +0.08em / uppercase`):** API path crumbs above
  page titles (e.g. `GET /api/config-entries`); scope-group labels. Rendered
  in `fg.caption` to keep visual weight low.
- **Code (`12.5px / 400`, Cascadia Code):** Every API-shaped token: entry
  keys, project IDs, scope dimensions, endpoint strings, `entityType` /
  `action` enum values, token prefixes, variable names. Monospace is the
  primary legibility affordance — it makes "the thing an API would return"
  instantly distinguishable from prose.

Type size never changes between Comfortable and Compact densities; only
vertical padding tightens.

## Layout

The shell follows a **fixed sidebar + fluid content pane** model with a hard
maximum content width of **1280px**.

- **Sidebar:** 232px wide, full viewport height. Contains navigation, the
  product mark, and the system health footer.
- **Header:** 56px tall, spans the content pane. Holds the global search, the
  live telemetry pill, notifications, and the user avatar.
- **Content pane:** The remaining space. Each screen uses a **two-column
  layout** (typically `1fr / 460px` for list + detail panels, or `360px / 1fr`
  for narrow list + wide detail). The Configuration JSON mode and Onboarding
  screen use full-width single-column layouts as deliberate exceptions.

**Spacing scale** is multiples of 4px (`4 / 8 / 12 / 16 / 20 / 24 / 32px`).
Page heads break from the scale with `36px` vertical and `56px` horizontal
padding — a deliberate editorial step that gives titles room to breathe and
signals a structural boundary between navigation and content.

**Density:** Comfortable (default) uses the full spacing scale. Compact
tightens list rows, page head padding, and card internals while keeping type
sizes unchanged and the grid breakpoints intact.

## Elevation & Depth

Depth is expressed through **tonal layering**, not shadows.

- `bg.page` (`#fcfcfc` light / `#252526` dark) is the base.
- `bg.container` (slightly inset, `#f8f7fa` light / `#252526` dark) sits
  *behind* cards, giving sections a recessed feeling.
- `bg.surface` (white / `#2d2d30` dark) raises cards and inputs one tonal step.

Shadows are used only for **floating** contexts: the Tweaks panel and
dropdowns use `0 18px 40px −16px rgba(0,0,40,.25)`. Modal overlays use a
deeper `0 30px 70px −20px rgba(0,0,40,.45)`. Nothing else casts a shadow;
structure is read through colour contrast alone.

Selected rows signal elevation through `bg.selected` (soft translucent
violet) and a **2px accent left border**, not a shadow.

## Shapes

The shape language is **architecturally sharp with minimal softening**.

- `3px` (sm) — inline code chips, scope badges: nearly-square pills that
  signal "this is data, not UI chrome."
- `4px` (md) — pills, small badges, small buttons: just enough softening to
  read as "pill."
- `6px` (lg) — text inputs, nav items, secondary buttons.
- `8px` (xl) — cards, table containers: the primary content container radius.
- `10px` (2xl) — modal surfaces and floating panels.
- `9999px` (full) — primary and secondary action buttons. Fully rounded
  buttons contrast with the sharper structural containers, creating a visual
  hierarchy between "content" and "action."

Never mix fully-rounded buttons with rounded-rectangular containers on the
same interactive element. The distinction between `pill` shapes (actions) and
`xl` shapes (containers) must be preserved.

## Components

### Buttons

**Primary:** Fully rounded pill (`full`), violet fill, white label, Inter
body 13px. Used for the single most important action per screen (Publish
snapshot, New project, New token). One primary button per page head.

**Secondary:** Fully rounded pill, transparent with a `neutral-300` border,
`neutral-700` label. Used for supporting actions (Filter, Compare to active,
Export CSV).

**Ghost:** Transparent, no border, `neutral-500` label, `lg` radius. Used
inside tables (Edit, Revoke) and as inline affordances. Appears on hover only
in dense contexts.

**Disabled state:** 40% opacity, cursor `not-allowed`. Never hide disabled
buttons — surfacing *why* they are disabled is the product's job (e.g.
"Continue" is disabled on the Publish modal's diff step when there are no
pending changes).

### Segmented Control

A pill-shaped tab group that switches view modes. Two sizes:
- **md** (`12.5px` label, default): Configuration view mode, Snapshot view
  mode.
- **sm** (`11.5px` label): Scope-dimension pickers inside JSON mode.

Sits on `neutral-100` with 3px inner padding. The active segment raises onto
`surface` with a 1px soft box-shadow and flips its label from `fg.caption` to
`fg.heading` at weight 600. Each option may carry an optional leading Phosphor
icon. The control persists its state to the matching `tweaks.*ViewMode`
localStorage key.

### Filter Chips

Row of mono chips used to filter tables (Clients, Audit trail, entity-type
filters). Default state: `neutral-100` background, `neutral-700` text.
Selected state **inverts** (`neutral-900` background, white text in light mode;
`neutral-50` background, near-black text in dark mode). This inverse chip
pattern ensures selection reads as "solid" with no ambiguity regardless of
colour mode.

### Badges / Pills

Five semantic variants — neutral, info, success, warning, critical — each
with a soft tinted background and its matching dark foreground text. Role
indicators use: critical = Admin, warning = Publisher, info = Editor, neutral
= Viewer. Status indicators use: success = active/live, neutral = disabled,
critical = revoked.

### Inputs

Single-line text inputs: `lg` radius, `neutral-300` initial border, accent-600
on focus (with a 2px accent focus ring), `surface` background. Placeholder
text uses `fg.caption`. Helper text sits below the field in `label-sm`.
Error state replaces the border with `error` and renders an error message
below.

### Navigation Items

Icon + label rows in the sidebar. Default: `neutral-600` label, transparent
background. Active: raised onto `surface`, `neutral-900` label at weight 600,
a 2px accent solid rail on the inner left edge. Nav items use `lg` radius.

### Cards

`surface` background, `xl` radius, `24px` internal padding. No intrinsic
shadow — depth comes from the `bg.container` tonal layer behind them.

### Code / Identifier Chips

Inline code fragments (entry keys, scope dimension `k=v` pairs, template IDs,
endpoint strings): `sm` radius (3px), `neutral-100` background, Cascadia Code
`12.5px`, `neutral-700` text. These are "data chips" — not interactive by
default, but may gain a hover affordance when they are copyable.

### JSON / Diff Surfaces

All JSON preview and diff views consume the `semantic.syntax.*` token set:

| Role | Light | Dark |
|---|---|---|
| Property key | Violet (`accent-700`) | `#9cdcfe` |
| String value | Forest green (`success-800`) | `#ce9178` |
| Number / boolean | Warm red (`error-700`) | `#b5cea8` |
| Punctuation | `neutral-500` | `#808080` |
| Sensitive (masked) | Amber (`warning-800`) | `#dcdcaa` |
| Diff addition (bg) | Translucent green | Translucent green |
| Diff deletion (bg) | Translucent red | Translucent red |

Sensitive values always render as `••••••••` coloured with `syntax.sensitive`,
accompanied by a trailing "sensitive" marker chip on `syntax.sensitiveBg`.
Component code never hard-codes hex; it always references the semantic token.

## Do's and Don'ts

- Do use the violet accent only for the single most important action per
  screen, focus rings, active navigation, and selected-row indicators.
- Don't use accent colour decoratively — it is a signal, not a brand splash.
- Do set all API-shaped identifiers (keys, IDs, scope dimensions, endpoint
  paths, enum values, token prefixes) in Cascadia Code. Prose and data must
  be visually distinct at a glance.
- Don't use `body` font for identifiers, even if they are short strings.
- Do maintain WCAG AA contrast ratios (4.5:1 for normal text). The neutral
  ramp and semantic token assignments have been chosen to meet this threshold
  in both light and dark mode.
- Don't use raw palette tokens in components. Always consume the semantic
  role tokens (`bg.*`, `fg.*`, `stroke.*`, `badge.*`).
- Do include a visible eyebrow + page title + subtitle on every screen. This
  is the primary orientation affordance.
- Don't omit the eyebrow API path — it is both a legibility aid and an
  implicit "read the docs" pointer.
- Do use the inverse chip pattern (dark-on-light → light-on-dark) for filter
  chip selected states so selection reads as solid in both colour modes.
- Don't mix fully-rounded pill buttons with fully-rounded containers for
  structural content — keep pill shape exclusively for actions.
- Do show current state (active snapshot version, current grants, current
  scope resolution) before presenting editing affordances.
- Don't hide disabled primary action buttons; surface the reason for the
  disabled state adjacent to the button.
- Do apply a reduced-motion path for all pulsing live-status dots.
- Don't use shadows for elevation on static content — tonal layering only.
  Shadows are reserved for floating UI (dropdowns, modals, the Tweaks panel).
