# Product

## Register

product

## Users

The Tower is used primarily by platform, infrastructure, and operations engineers who manage GroundControl on behalf of teams and services. They work in authenticated admin workflows where they need to inspect configuration state, publish snapshots, manage clients and access, and verify what changed without ambiguity.

## Product Purpose

The Tower is the operational control surface for GroundControl, a self-hosted, scope-aware configuration platform. It exists to let engineers manage projects, scopes, variables, templates, clients, users, tokens, and audit trails with enough clarity and trust that they can safely change live configuration, understand the blast radius of those changes, and confirm what the system will deliver to running applications.

## Brand Personality

Reserved, editorial, calm. Institutional, precise, and unhurried. The interface should feel authoritative and trustworthy rather than flashy, consumerized, or playful. Success looks like an engineer believing the UI is showing the truth, especially around active snapshots, scoped values, secrets, permissions, and audit history.

## Anti-references

Do not make this feel like a marketing site, a consumer SaaS dashboard, or an observability tool chasing drama through neon color, dense gradients, or decorative motion. Avoid playful illustration, growth-metric hero patterns, generic admin-template card farms, and any visual treatment that makes sensitive operational work feel casual or game-like.

## Design Principles

- Show system truth before action. Current state, active versions, scope boundaries, and masked-sensitive behavior should be legible before any edit or publish workflow begins.
- Make machine-shaped information first-class. IDs, keys, scopes, permissions, endpoints, and audit records are primary content, not secondary metadata.
- Prefer calm authority over visual novelty. Familiar product affordances, disciplined hierarchy, and restrained color should carry the experience.
- Optimize for safe operational flow. Engineers should be able to move quickly through repeat admin tasks without losing orientation or confidence.
- Earn trust with precision. Labels, permissions, destructive actions, and audit surfaces should remove ambiguity rather than relying on user inference.

## Accessibility & Inclusion

Target WCAG AA across the Tower surface. Keyboard navigation, visible focus states, semantic controls, and screen-reader-compatible labels are required on all interactive flows. Respect reduced-motion preferences, do not rely on color alone for status or permission meaning, and preserve readability for dense operational content such as code-like identifiers, diff views, and audit records.