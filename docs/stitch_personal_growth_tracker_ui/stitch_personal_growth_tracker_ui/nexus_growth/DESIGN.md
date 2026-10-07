---
name: Nexus Growth
colors:
  surface: '#faf8ff'
  surface-dim: '#d2d9f4'
  surface-bright: '#faf8ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f3ff'
  surface-container: '#eaedff'
  surface-container-high: '#e2e7ff'
  surface-container-highest: '#dae2fd'
  on-surface: '#131b2e'
  on-surface-variant: '#434655'
  inverse-surface: '#283044'
  inverse-on-surface: '#eef0ff'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#712ae2'
  on-secondary: '#ffffff'
  secondary-container: '#8a4cfc'
  on-secondary-container: '#fffbff'
  tertiary: '#006058'
  on-tertiary: '#ffffff'
  tertiary-container: '#007b71'
  on-tertiary-container: '#b3fff3'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#eaddff'
  secondary-fixed-dim: '#d2bbff'
  on-secondary-fixed: '#25005a'
  on-secondary-fixed-variant: '#5a00c6'
  tertiary-fixed: '#89f5e7'
  tertiary-fixed-dim: '#6bd8cb'
  on-tertiary-fixed: '#00201d'
  on-tertiary-fixed-variant: '#005049'
  background: '#faf8ff'
  on-background: '#131b2e'
  surface-variant: '#dae2fd'
typography:
  display:
    fontFamily: Inter
    fontSize: 36px
    fontWeight: '700'
    lineHeight: 44px
    letterSpacing: -0.025em
  display-mobile:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 36px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.02em
  headline-lg-mobile:
    fontFamily: Inter
    fontSize: 22px
    fontWeight: '600'
    lineHeight: 30px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.015em
  headline-sm:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: -0.01em
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 26px
    letterSpacing: -0.005em
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 22px
    letterSpacing: 0em
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
    letterSpacing: 0em
  label-md:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '600'
    lineHeight: 14px
    letterSpacing: 0.03em
  metric-lg:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 32px
    letterSpacing: -0.02em
  metric-md:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: -0.01em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1.5rem
  gutter-mobile: 0.75rem
  margin: 2rem
  margin-mobile: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
---

## Brand & Style

This design system delivers an executive-grade personal development workspace tailored for ambitious professionals seeking structured progress across every dimension of life. Balancing clinical clarity with human warmth, the aesthetic avoids gamified clutter in favor of intentional, high-density dashboard precision. 

The core style pairs **Minimalist Functionalism** with **Modern Data-Dense Utility**:
- **Clarity Over Novelty:** Ultra-clean canvases with disciplined whitespace and crisp micro-borders allow multiple parallel life vectors to coexist without cognitive fatigue.
- **Calibrated Feedback:** Progress indicators and scoring thresholds provide direct, objective feedback through authoritative status signifiers rather than noisy celebratory tropes.
- **Bilingual Typographic Rigor:** Engineered with optimized metric support for Vietnamese diacritics, ensuring balance, accent alignment, and vertical harmony across multi-lingual self-improvement tracking.

## Colors

The palette establishes strict semantic clarity, anchoring performance metrics and goal categories into dedicated chromatic swimlanes.

### Core Foundation
- **Canvas (`#F8FAFC`):** Soft cool-slate backing providing a glare-free baseline for extended planning and review sessions.
- **Surfaces (`#FFFFFF`):** Pure white container surfaces that anchor content blocks against the slate backdrop.
- **Borders & Dividers (`#E2E8F0`):** Precise 1px boundaries defining structural hierarchy without visual weight.
- **Text & Neutral (`#0F172A`):** Deep ink slate offering high-contrast readability across all display scales.

### Domain Taxonomy
Categories maintain fixed chromatic assignments across tags, chart nodes, progress tracks, and priority markers:
- **Career (Sự nghiệp):** `#2563EB`
- **English (Tiếng Anh):** `#7C3AED`
- **Health (Sức khỏe):** `#16A34A`
- **Finance (Tài chính):** `#EA580C`
- **Knowledge (Tri thức):** `#0D9488`
- **Personal (Cá nhân):** `#DB2777`

### Performance Score Tiers
System metrics and progress rubrics evaluate directly against five performance tiers:
- **Xuất sắc (Excellent):** `#15803D`
- **Tốt (Good):** `#16A34A`
- **Đạt (Pass):** `#CA8A04`
- **Cần cải thiện (Needs Improvement):** `#EA580C`
- **Yếu (Weak):** `#DC2626`

## Typography

The typography uses Inter across every hierarchical tier, configured for maximum scannability and balanced vertical rhythm.

- **Tabular Figures (`tnum`):** All performance scores, completion percentages, XP meters, and time counts mandate the CSS feature setting `font-feature-settings: "tnum" on, "cv05" on`. This guarantees character width consistency during value updates, preserving table alignments.
- **Vietnamese Accent Handling:** Extended line-height multiples (minimum 1.45x for running copy) prevent tonal diacritics (dấu ngã, dấu hỏi, dấu nặng) from clipping against container edges or overlapping previous lines.
- **Uppercase Discipline:** `label-sm` applies uppercase styling strictly to status indicators, category micro-labels, and column sorting headers.

## Layout & Spacing

The layout is built upon an 8pt modular rhythm on a fluid 12-column grid.

- **Desktop (1280px+):** Fixed lateral navigation bar (260px) alongside a 12-column fluid workspace. Column gutters rest at `1.5rem` (`24px`), with outer canvas padding set to `2rem` (`32px`). Multi-metric cards utilize 3-column, 4-column, or 6-column spans.
- **Tablet (768px - 1024px):** Navigation collapses to an icon rail or sheet overlay. Columns reduce to 6 or 8 units; inner card layout stacks secondary analytics beneath primary progress rings.
- **Mobile (320px - 767px):** Single-column stack. Outer margins condense to `1rem` (`16px`), gutters tighten to `0.75rem` (`12px`), and complex metric grids reflow into swipeable carousels or stacked micro-cards.

## Elevation & Depth

Visual hierarchy avoids heavy drop shadows, relying primarily on crisp low-contrast borders combined with faint ambient occlusion:

- **Level 0 (Canvas):** Flat `#F8FAFC` background.
- **Level 1 (Card Default):** `#FFFFFF` surface bounded by a crisp `1px solid #E2E8F0` border. Zero vertical shadow (`box-shadow: 0 1px 2px 0 rgba(15, 23, 42, 0.03)`).
- **Level 2 (Hover & Active Surfaces):** Elevated on pointer focus to `box-shadow: 0 4px 12px -2px rgba(15, 23, 42, 0.06), 0 2px 4px -2px rgba(15, 23, 42, 0.04)`, with border tone stepping to `#CBD5E1`.
- **Level 3 (Modals, Overlays, Dropdowns):** Floated elements use `box-shadow: 0 12px 24px -4px rgba(15, 23, 42, 0.08), 0 4px 6px -2px rgba(15, 23, 42, 0.03)` and maintain the distinct `#E2E8F0` edge perimeter.

## Shapes

The geometric framework establishes clear distinctions based on component function:

- **Cards & Data Panels:** Exact `rounded-xl` (`12px`) geometry provides a smooth structural contour that separates distinct productivity modules.
- **Interactive Controls (Buttons, Inputs, Selectors):** Uniform `rounded-lg` (`8px`) radii provide ergonomic affordance for functional inputs.
- **Pills, Badges & Avatars:** Fully circular `rounded-full` (`9999px`) geometries are reserved exclusively for domain badges, status markers, count indicators, and profile avatars.

## Components

### Buttons
- **Primary:** Solid fill (default `#2563EB`), high-contrast white text, `rounded-lg` (`8px`), height `40px` (desktop) or `44px` (mobile). Hover state applies a 6% dark tone overlay; active state applies a 12% overlay.
- **Secondary / Ghost:** `#FFFFFF` fill with `1px solid #E2E8F0`, slate `#0F172A` text. Hover shifts background to `#F8FAFC` and border to `#CBD5E1`.
- **Action Icons:** Integrated trailing or leading SVG icons sized to `16x16px` with an inline gap of `8px`.

### Status Badges & Category Chips
- **Category Chips:** `rounded-full`, padding `4px 10px`, typography `label-sm`. Filled with a 10% opacity tint of the respective domain color, pairing with an opaque solid text label in the same hue.
- **Score Indicators:** Features a solid `6px` status dot paired with high-contrast score text (e.g., `#15803D` dot for "Xuất sắc").

### Progress Rings & Streak Bars
- **Track:** Background tracks use `#F1F5F9` at a stroke width of `6px` or `8px`.
- **Indicator:** Direct fill using the domain or score color token with smooth transitions (`transition: stroke-dashoffset 400ms ease-out`).

### Data Cards
- Container constructed with `#FFFFFF`, `12px` roundedness, and a `1px solid #E2E8F0` border.
- Internal padding is set to `20px` (`space-lg`), separating header titles, tabular numbers, and trailing visual graphs cleanly.
- Interactive cards elevate smoothly on hover with a 150ms transition.

### Input Fields & Controls
- Form inputs feature a `40px` height, `#FFFFFF` background, `1px solid #E2E8F0` border, and `rounded-lg` (`8px`) shape.
- Active focus displays a crisp 2px focus ring (`box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.2)`), preserving accessible focus outlines without obscuring border definitions.
- Checkboxes utilize `rounded` (`4px`) corners with a checked fill matching the category color token.