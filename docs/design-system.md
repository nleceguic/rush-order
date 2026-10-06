# Rush Order — Design System

**Propósito:** documento de referencia técnica para diseñar cada pantalla del Screen Manifest del panel de escritorio. Contiene los tokens (color, tipografía, espaciado, radios) y la especificación de los componentes base con sus estados.

**Fecha de generación:** 2026-08-22
**Fuente de los valores:** `desktop/src/RushOrder.Desktop/Theme/ThemeManager.cs` y `pwa/tailwind.config.ts` — los tokens de marca y el modo oscuro no son inventados, están tomados del código ya en producción.

**Marca:** Rush Order — *"Tu mesa, tu ritmo"*. SaaS de pedidos por QR para restaurantes. Este documento cubre el panel de gestión de escritorio (WinForms, .NET 8), pensado para leerse de un vistazo en cocina y para decisiones rápidas sin fricción de interfaz.

---

## Índice

1. [Color](#1-color)
2. [Tipografía](#2-tipografía)
3. [Espaciado y grid](#3-espaciado-y-grid)
4. [Componentes](#4-componentes)
   - [4.1 Botones](#41-botones)
   - [4.2 Inputs y selects](#42-inputs-y-selects)
   - [4.3 Cards de pedido y mesa](#43-cards-de-pedido-y-mesa)
   - [4.4 Badges de estado](#44-badges-de-estado)
   - [4.5 Tablas y listas](#45-tablas-y-listas)
   - [4.6 Modales](#46-modales)
   - [4.7 Toasts](#47-toasts)
5. [Iconografía](#5-iconografía)
6. [Cómo aplicarlo](#6-cómo-aplicarlo)

---

## 1. Color

### Regla 60/30/10

| Rol | Proporción | Uso | Color |
|---|---|---|---|
| Dominante | 60% | Superficie de trabajo — fondos y paneles neutros | Neutros de tema (ver [modo oscuro](#modo-oscuro--cocina-con-poca-luz)) |
| Secundario | 30% | Estructura y navegación — sidebar, headers, botón secundario | Rush Dark `#1D3557` |
| Acento | 10% | Acción y estado — CTA, elementos activos | Rush Red `#E63946` |

El azul marino domina la navegación porque así se usa ya en `ThemeManager.SidebarBg` (oscuro incluso en tema claro). El rojo se reserva para acción y estado — nunca para grandes superficies.

### Paleta de marca

| Nombre | Token CSS | Hex | Uso |
|---|---|---|---|
| Rush Red | `--rush-red` | `#E63946` | Acento — CTA primario, estado activo |
| Rush Red · hover | `--rush-red-hover` | `#C1121F` | Hover/active de botones primarios |
| Rush Dark | `--rush-dark` | `#1D3557` | Secundario — sidebar, headers, botón secundario |
| Rush Dark · light | `--rush-dark-light` | `#2D4A6E` | Hover sobre superficies oscuras |
| Rush Blue | `--rush-blue` | `#457B9D` | Terciario — datos, gráficas, estado "nuevo" |
| Rush Mint | `--rush-mint` | `#F1FAEE` | Fondo alterno claro, resaltes suaves |

### Colores semánticos

| Nombre | Token CSS | Hex | Uso |
|---|---|---|---|
| Éxito | `--success` | `#4CAF50` | Confirmaciones, pagos completados |
| Advertencia | `--warning` | `#FF9800` | Estados que requieren atención pronto |
| Error | `--error` | `#F44336` | Validación fallida, cancelaciones |
| Información | `--info` | `#2196F3` | Avisos neutros |

> **Regla:** el rojo de marca (`#E63946`) y el rojo de error (`#F44336`) son deliberadamente distintos y **no son intercambiables**. El rojo de marca es para *acción* (botones, foco); el rojo semántico es para *fallo* (validación, cancelaciones).

### Modo oscuro — cocina con poca luz

Paleta real de `ThemeManager.cs`, formalizada aquí. Se usa por defecto en pantallas de alto contraste rápido (p. ej. KDS-01, cocina).

| Token | Rol | Tema claro | Tema oscuro |
|---|---|---|---|
| `--d-bg` | Background | `#F8F8F8` | `#121212` |
| `--d-surface` | Surface | `#FFFFFF` | `#1D1D1D` |
| `--d-sidebar` | SidebarBg | `#1D1D1D` | `#0F0F0F` |
| `--d-header` | HeaderBg | `#FFFFFF` | `#1D1D1D` |
| `--d-text` | TextPrimary | `#1D1D1D` | `#F0F0F0` |
| `--d-text2` | TextSecondary | `#787878` | `#A0A0A0` |
| `--d-border` | Border | `#E5E5E5` | `#323232` |
| `--d-input` | Input | `#FFFFFF` | `#282828` |

`NavActive` (color del ítem de navegación seleccionado) usa `Rush Red` (`#E63946`) en ambos temas.

---

## 2. Tipografía

Un único tipo de letra — **Poppins** (ya incrustada en la app de escritorio: pesos 400/500/600/700) — con 3 tamaños y 2 pesos para texto de interfaz. Sin cursivas, sin familias adicionales salvo la excepción tabular de abajo.

| Uso | Tamaño | Peso | Line-height | Ejemplo de clase/estilo |
|---|---|---|---|---|
| Título de pantalla | 22px (`1.375rem`) | 600 | 1.2 | `.block-head h2` |
| Cuerpo / subtítulo | 15px (`0.9375rem`) | 400 | 1.6 | texto base del documento |
| Cuerpo — énfasis | 15px (`0.9375rem`) | 600 | 1.6 | totales, valores destacados |
| Label / badge | 12px (`0.75rem`) | 600, mayúsculas, `letter-spacing: .06em` | 1 | `.badge`, labels de campo |

### Excepciones — justificadas, fuera de la escala de 3

Documentadas aparte porque resuelven un problema real de legibilidad rápida, no por gusto.

| Uso | Tamaño | Peso | Fuente | Notas |
|---|---|---|---|---|
| Cifra clave (KPI / cocina) | 34px (`2.125rem`) | 700 | Poppins | Contadores de dashboard, cifras grandes de un vistazo |
| Dato tabular (id, hora, precio) | 13.5px (`0.84375rem`) | 500 | IBM Plex Mono | `font-variant-numeric: tabular-nums` — para escanear dígitos rápido (ids de pedido, horas, importes en tablas) |

---

## 3. Espaciado y grid

Escala de 8px con un paso intermedio de 4px para ajustes finos (iconos, badges). Todo margen o padding sale de esta lista — nada de valores sueltos.

| Token | Valor |
|---|---|
| `space-1` | 4px |
| `space-2` | 8px |
| `space-3` | 16px |
| `space-4` | 24px |
| `space-5` | 32px |
| `space-6` | 48px |
| `space-7` | 64px |

### Radios de borde

Confirmados en el código actual (llamadas a `RoundCorners` en los diálogos de escritorio).

| Token | Valor | Uso |
|---|---|---|
| `radius-sm` | 8px | Botones, inputs |
| `radius-lg` | 16px | Cards, modales |
| `radius-pill` | 999px | Badges, filtros |

---

## 4. Componentes

### 4.1 Botones

Estilo base común a las tres variantes: `font-family: Poppins; font-weight: 600; font-size: 13.5px; border-radius: 8px (radius-sm); padding: 10px 20px; border: 1.5px solid transparent;`

| Variante | Estado | Fondo | Texto/Borde | Clase modificadora |
|---|---|---|---|---|
| Primario (`.btn.primary`) | Default | `#E63946` (Rush Red) | Texto `#FFFFFF` | — |
| | Hover | `#C1121F` (Rush Red hover) | Texto `#FFFFFF` | `:hover`, `.is-hover` |
| | Active | `#9E0E1A` + `transform: scale(.98)` | Texto `#FFFFFF` | `.is-active` |
| | Disabled | `#E9A9AE` | Texto `#FFFFFF`, `cursor: not-allowed` | `.is-disabled` |
| Secundario (`.btn.secondary`) | Default | Transparente | Texto y borde `#1D3557` (Rush Dark) | — |
| | Hover | `#1D3557` | Texto `#FFFFFF` | `:hover`, `.is-hover` |
| | Active | `#132741` + `scale(.98)` | Texto `#FFFFFF` | `.is-active` |
| | Disabled | Transparente | Texto `var(--faint)`, borde `var(--border-strong)` | `.is-disabled` |
| Destructivo (`.btn.destructive`) | Default | Transparente | Texto y borde `#F44336` (Error) | — |
| | Hover | `#F44336` | Texto `#FFFFFF` | `:hover`, `.is-hover` |
| | Active | `#B81F16` | Texto `#FFFFFF` | `.is-active` |
| | Disabled | Transparente | Texto `#F2B3AD`, borde `#F6D3CE` | `.is-disabled` |

Uso: primario para la acción principal de la pantalla (p. ej. "Cobrar", "Enviar a cocina"); secundario para acciones alternativas ("Ver detalle", "Editar"); destructivo solo para acciones irreversibles ("Cancelar pedido", "Eliminar").

### 4.2 Inputs y selects

Estilo base: `font-family: Poppins; font-size: 14px; padding: 10px 12px; border-radius: 8px (radius-sm); border: 1.5px solid var(--border-strong);`

| Estado | Especificación | Clase/selector |
|---|---|---|
| Default | Borde `var(--border-strong)`, fondo `var(--surface)` | `.field input` |
| Focus | Borde `#E63946` (Rush Red), `box-shadow: 0 0 0 3px` sobre `--accent-soft` | `.field input:focus` |
| Error | Borde `#F44336` (Error); mensaje bajo el campo en `#F44336`, 11.5px | `.field.error input` / `.err-msg` |
| Disabled | Fondo `var(--surface-alt)`, texto `var(--faint)`, `cursor: not-allowed` | `.field input:disabled` |

Labels de campo: 11px, IBM Plex Mono, mayúsculas, `letter-spacing: .05em`, color `var(--muted)`.

### 4.3 Cards de pedido y mesa

**Card de pedido** (`.order-card`) — `border-radius: 16px (radius-lg); padding: 16px; border: 1px solid var(--border);`

Estructura:
- `.top` — fila superior: id de pedido en mono (`.oid`, `#0847` estilo) + número de mesa en negrita 15px, badge de estado alineado a la derecha.
- `ul` de líneas del pedido — 13px, color `var(--muted)`.
- `.foot` — pie con separador punteado (`border-top: 1px dashed var(--border-strong)`): precio en mono 600 14px (`.price`) y antigüedad del pedido en mono 11px `var(--faint)` (`.age`).

**Card de mesa** (`.table-card`) — mismo radio y borde, contenido centrado: número de mesa grande en mono 26px/600 (`.num`), aforo en 12px muted (`.cap`), badge de estado de la mesa debajo (`.stat`).

### 4.4 Badges de estado

Mapeados 1:1 al enum `OrderStatus` del panel de escritorio (`New`, `Preparing`, `Ready`, `Served`, `Paid`) más `Cancelled` del backend. Cada estado tiene un único color — no se reutiliza entre estados.

| Estado | Clase | Fondo | Texto |
|---|---|---|---|
| Nuevo | `.badge.b-new` | `#E4EEF4` | `#457B9D` (Rush Blue) |
| En preparación | `.badge.b-prep` | `#FFF3E0` | `#B96A00` |
| Listo | `.badge.b-ready` | `#E7F5E8` | `#2E7D32` |
| Entregado | `.badge.b-served` | `#EFECE8` | `#6B6459` |
| Cobrado | `.badge.b-paid` | `#E3E8EE` | `#1D3557` (Rush Dark) |
| Cancelado | `.badge.b-cancel` | `#FDECEB` | `#C62828` |

Estilo base: `font-family: IBM Plex Mono; font-size: 11px; font-weight: 600; letter-spacing: .03em; text-transform: uppercase; padding: 5px 12px; border-radius: 999px (radius-pill);` con un punto de 6px (`currentColor`) antes del texto.

### 4.5 Tablas y listas

Contenedor `.table-wrap`: `overflow-x: auto; border: 1px solid var(--border); border-radius: 12px;` (permite scroll horizontal sin romper el layout de la página).

`table.data`:
- `thead th` — IBM Plex Mono, 10.5px, mayúsculas, `letter-spacing: .07em`, color `var(--faint)`, fondo `var(--surface-alt)`.
- `tbody td` — padding `11px 16px`, borde inferior `1px solid var(--border)`.
- Hover de fila: fondo `var(--surface-alt)`.
- Columnas numéricas (`td.num`): IBM Plex Mono, `font-variant-numeric: tabular-nums`.

### 4.6 Modales

`.modal-frame` — contenedor de previsualización, fondo `var(--surface-alt)`, padding 34px, centrado.

`.modal` — `max-width: 340px; background: var(--surface); border-radius: 16px (radius-lg); box-shadow: 0 20px 50px rgba(0,0,0,.18);`

- `.modal-head` — padding `18px 20px`, borde inferior, título 15px.
- `.modal-body` — padding `18px 20px`, texto 13.5px `var(--muted)`.
- `.modal-foot` — padding `14px 20px`, alineado a la derecha, `gap: 10px`, borde superior. Acciones típicas: botón secundario ("Volver") + botón destructivo o primario según el contexto.

### 4.7 Toasts

`.toast-stack` — columna, `gap: 10px`, `max-width: 320px`.

`.toast` — `display: flex; background: var(--surface); border: 1px solid var(--border); border-left: 4px solid <color>; border-radius: 10px; padding: 12px 14px;` con título en negrita (13px) y descripción en `var(--muted)` (12.5px).

| Tipo | Clase | Color de borde izquierdo |
|---|---|---|
| Informativo (default) | `.toast` | `#457B9D` (Rush Blue) |
| Éxito | `.toast.success` | `#4CAF50` (Éxito) |
| Error | `.toast.error` | `#F44336` (Error) |

---

## 5. Iconografía

**Estilo:** trazo (*outline*), sin relleno, grosor `1.6px`, `stroke-linecap: round`, `stroke-linejoin: round`, sobre grid de 24×24px (`viewBox="0 0 24 24"`).

> Sustituye a los glifos Unicode provisionales que hoy usa la barra lateral (⊞ ⬛ ≡ ◈ ☰ ♟ ◻ ≈ ◇ ✦) — esos no son un sistema de iconos, son marcadores de posición y deben reemplazarse por el estilo outline documentado aquí.

Set mínimo cubierto en la referencia visual: **Mesas, Pedidos, Cocina, Menú, Panel, Estadísticas, Tiempo, Previsión, Listo (check), Sin conexión, Impresora/caja.** Ampliar este set siguiendo siempre la misma especificación de trazo y grid.

---

## 6. Cómo aplicarlo

- ✅ Cada pantalla del Screen Manifest se construye solo con los tokens de este documento — ningún color, tamaño o radio nuevo por pantalla.
- ✅ La pantalla de cocina (KDS) usa por defecto el modo oscuro de la sección 1 — es la que más se beneficia de poca luz y lectura a distancia.
- ✅ Los 6 badges de estado (sección 4.4) son la única forma válida de mostrar el estado de un pedido — no texto libre, no iconos sueltos.
- ❌ No usar Rush Red para mensajes de error — ese uso es exclusivo del rojo semántico `#F44336`.
- ❌ No introducir un cuarto tamaño de texto sin documentarlo aquí primero, salvo las dos excepciones ya justificadas en la sección 2.
