## Conventions

**Setup.** No root provider or theme wrapper is required — components style themselves entirely through Tailwind utility classes on `className`, so `<Button variant="primary">` works standalone with no context setup. The one exception: `SkipLink` and `PwaInstallBanner` call `useTranslation()` (react-i18next) and render raw translation keys (e.g. `pwa.installTitle`) unless an i18next instance has been initialized in the host app — real usage always needs the app's own i18n setup already running; there is no prop to pass translated text directly.

**Styling idiom — Tailwind utility classes, brand tokens as Tailwind theme colors, no CSS-in-JS.** Every component composes real Tailwind classes; there is no separate component stylesheet or class-name API to learn beyond Tailwind's own. Brand colors are Tailwind theme extensions, used exactly like any other Tailwind color:

| Token | Classes | Hex |
|---|---|---|
| Rush Red (primary/accent) | `bg-rush-red`, `text-rush-red`, `border-rush-red`, `hover:bg-rush-red-hover` | `#E63946` (hover `#C1121F`) |
| Rush Dark (secondary) | `bg-rush-dark`, `text-rush-dark`, `border-rush-dark`, `hover:bg-rush-dark-light` | `#1D3557` (hover `#2D4A6E`) |

Font is Poppins everywhere (`font-family: Poppins, system-ui, sans-serif`, loaded via `@fontsource/poppins` — ships as real `@font-face` rules in `fonts/`). Semantic/status colors (success green, warning yellow, danger/error red, info blue) are plain Tailwind defaults (`bg-green-100`/`text-green-700`, etc.) — Rush Red is reserved for brand/primary actions, never for error states (see `Badge`'s `danger` variant, which uses Tailwind red, not `rush-red`).

**Where the truth lives.** Read `styles.css` (imports `_ds_bundle.css`, the full compiled utility set) before styling anything new — it is the authoritative list of what's actually shipped. Each component's `.prompt.md` documents its own props; there is no separate design-guidelines doc beyond that.

**Build snippet** (real, from this sync's own preview — see `components/general/Button/Button.prompt.md` and `components/general/Modal/Modal.prompt.md` for the full APIs):

```tsx
<Modal open={open} onClose={handleClose} title="Cancelar pedido">
  <p className="text-sm text-gray-600 mb-4">
    ¿Seguro que quieres cancelar el pedido #0847? Esta acción no se puede deshacer.
  </p>
  <div className="flex justify-end gap-2">
    <Button variant="ghost" onClick={handleClose}>Volver</Button>
    <Button variant="danger" onClick={handleConfirm}>Cancelar pedido</Button>
  </div>
</Modal>
```

For a new layout that isn't one of the 7 shipped components, use the same idiom directly: Tailwind utility classes, `rush-red`/`rush-dark` for brand color, Poppins for type — never invent a new class-name vocabulary.
