# Guia de Implementacion — Sistema de Temas de Walos

> **Objetivo**: Que un agente AI (u otro desarrollador) pueda replicar EXACTAMENTE el sistema de seleccion de temas que tiene Walos, sin ambiguedades ni margen de error.
>
> **Alcance**: SOLO el sistema de temas visuales. NO incluye branding (logo/nombre), descuentos ni otras configuraciones.

---

## INDICE

1. [Resumen del Flujo](#1-resumen-del-flujo)
2. [Stack y Dependencias](#2-stack-y-dependencias)
3. [Arquitectura del Tema (como funciona)](#3-arquitectura-del-tema)
4. [Los 6 Temas Disponibles](#4-los-6-temas-disponibles)
5. [ARCHIVO 1: tailwind.config.js](#5-archivo-1-tailwindconfigjs)
6. [ARCHIVO 2: src/index.css](#6-archivo-2-srcindexcss)
7. [ARCHIVO 3: src/stores/uiStore.js (Zustand)](#7-archivo-3-srcstoresuistorejs)
8. [ARCHIVO 4: ThemeSync en App.jsx](#8-archivo-4-themesync-en-appjsx)
9. [ARCHIVO 5: ThemeSelector.jsx (Componente UI)](#9-archivo-5-themeselectorjsx)
10. [ARCHIVO 6: Integracion en la pagina de Settings](#10-archivo-6-integracion-en-la-pagina-de-settings)
11. [ARCHIVO 7: Persistencia en Backend](#11-archivo-7-persistencia-en-backend)
12. [ARCHIVO 8: Layout.jsx (carga inicial del tema)](#12-archivo-8-layoutjsx)
13. [Reglas que NO debes romper](#13-reglas-que-no-debes-romper)
14. [Checklist de verificacion](#14-checklist-de-verificacion)

---

## 1. Resumen del Flujo

```
Usuario selecciona tema en UI
        |
        v
ThemeSelector.jsx llama onSelect(themeId)
        |
        v
SettingsPage.jsx ejecuta:
  1. setForm({ themePreference: themeId })  ← estado local
  2. setTheme(themeId)                      ← Zustand store (cambio inmediato visual)
        |
        v
ThemeSync (en App.jsx) detecta cambio en store
  → document.documentElement.setAttribute('data-theme', themeId)
        |
        v
CSS en index.css reacciona via [data-theme='dark'] { ... }
  → Todas las CSS variables cambian
  → Tailwind primary-* colores cambian
  → Overrides de gray-*, green-*, red-* etc se activan para temas oscuros
        |
        v
Usuario hace clic en "Guardar cambios"
  → PUT /api/v1/company/settings { themePreference: themeId }
  → Backend guarda en BD campo theme_preference de la tabla companies
        |
        v
Proxima vez que cualquier usuario abre la app:
  Layout.jsx carga settings → lee themePreference → setTheme()
```

**CRITICO**: El cambio visual es INSTANTANEO (via Zustand + data-theme). La persistencia en BD es al guardar. Si el usuario cambia tema y no guarda, al recargar vuelve al anterior.

---

## 2. Stack y Dependencias

| Tecnologia | Version | Uso |
|---|---|---|
| React | 18+ | UI |
| Tailwind CSS | 3.x | Estilos (con CSS variables) |
| Zustand | 4.x | Estado global del tema |
| zustand/middleware/persist | - | Persistir tema en localStorage |
| lucide-react | latest | Iconos (Check para tema activo) |
| react-hot-toast | latest | Notificaciones |
| @tanstack/react-query | 5.x | Cache de settings del servidor |
| .NET 8 + Dapper | - | Backend API |

**Paquetes npm necesarios** (si no existen):
```bash
npm install zustand @tanstack/react-query lucide-react react-hot-toast
```

---

## 3. Arquitectura del Tema

### 3.1 Mecanismo

El sistema usa **CSS custom properties** (variables) que cambian segun el atributo `data-theme` en `<html>`.

```
<html data-theme="dark">   ← este atributo controla TODO
  <body>                    ← body usa var(--surface-base) como fondo
    <div class="card">      ← .card usa var(--surface-card) como fondo
      <p class="text-gray-900"> ← override CSS hace que use var(--text-strong)
```

### 3.2 Variables CSS

Cada tema define EXACTAMENTE estas 13 variables:

| Variable | Proposito | Ejemplo light | Ejemplo dark |
|---|---|---|---|
| `--color-primary-50` | Primary tint mas claro | `227 242 253` | `14 28 42` |
| `--color-primary-100` | Primary tint | `187 222 251` | `20 46 67` |
| `--color-primary-200` | Primary tint | `144 202 249` | `14 116 144` |
| `--color-primary-300` | Primary shade | `100 181 246` | `6 182 212` |
| `--color-primary-400` | Primary shade | `66 165 245` | `34 211 238` |
| `--color-primary-500` | **Primary base** | `26 115 232` | `34 211 238` |
| `--color-primary-600` | Primary hover | `21 101 192` | `8 145 178` |
| `--color-primary-700` | Primary dark | `13 71 161` | `14 116 144` |
| `--color-primary-800` | Primary darker | `10 61 145` | `21 94 117` |
| `--color-primary-900` | Primary darkest | `6 50 112` | `8 47 73` |
| `--surface-base` | Fondo de pagina | `249 250 251` | `10 15 25` |
| `--surface-card` | Fondo de cards/modals | `255 255 255` | `17 24 39` |
| `--surface-soft` | Fondo secundario | `249 250 251` | `30 41 59` |
| `--border-subtle` | Bordes suaves | `229 231 235` | `51 65 85` |
| `--border-strong` | Bordes fuertes | `209 213 219` | `71 85 105` |
| `--text-strong` | Texto principal | `17 24 39` | `241 245 249` |
| `--text-default` | Texto normal | `55 65 81` | `203 213 225` |
| `--text-muted` | Texto secundario | `107 114 128` | `148 163 184` |

**FORMATO**: Los valores son `R G B` separados por espacios (SIN comas, SIN rgb()). Esto permite que Tailwind los use con opacidad: `rgb(var(--color-primary-500) / 0.5)`.

### 3.3 Como Tailwind consume las variables

En `tailwind.config.js`, los colores `primary-*` se definen asi:
```js
primary: {
  500: 'rgb(var(--color-primary-500) / <alpha-value>)',
  // ...
}
```

Esto genera clases como `bg-primary-500`, `text-primary-600` etc. que automaticamente usan la variable CSS del tema activo.

### 3.4 Clases Tailwind estáticas que se redirigen

Para que clases como `text-gray-900` o `bg-white` tambien cambien con el tema, hay **overrides CSS** en `index.css`:

```css
[data-theme] .bg-white {
  background-color: rgb(var(--surface-card)) !important;
}
[data-theme] .text-gray-900 {
  color: rgb(var(--text-strong)) !important;
}
```

Esto significa que cualquier componente que use `text-gray-900` automaticamente se adapta a TODOS los temas sin cambiar su JSX.

---

## 4. Los 6 Temas Disponibles

| ID | Nombre | Tipo | Descripcion |
|---|---|---|---|
| `light` | Claro | Claro | Default. Azul corporate, superficies blancas |
| `dark` | Oscuro | Oscuro | Superficies profundas (slate), acento cyan |
| `grayscale` | Escala de grises | Claro | Monocromatico, zinc/neutral |
| `neon` | Neon | Oscuro | Superficies oscuras, acentos fuchsia/cyan |
| `pink` | Pink | Claro | Rosa calido, superficies blancas con tintes rosas |
| `purple` | Morado | Claro | Violeta elegante, superficies con tintes violeta |

**Temas oscuros** (`dark`, `neon`): Requieren overrides adicionales para colores semanticos (green, yellow, red, blue, orange) y sombras. Esto se maneja con selectores `:is([data-theme='dark'], [data-theme='neon'])`.

---

## 5. ARCHIVO 1: tailwind.config.js

**Ruta**: `frontend/tailwind.config.js`

Copia EXACTA (no modificar si ya existe con estos valores):

```js
/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        primary: {
          50: 'rgb(var(--color-primary-50) / <alpha-value>)',
          100: 'rgb(var(--color-primary-100) / <alpha-value>)',
          200: 'rgb(var(--color-primary-200) / <alpha-value>)',
          300: 'rgb(var(--color-primary-300) / <alpha-value>)',
          400: 'rgb(var(--color-primary-400) / <alpha-value>)',
          500: 'rgb(var(--color-primary-500) / <alpha-value>)',
          600: 'rgb(var(--color-primary-600) / <alpha-value>)',
          700: 'rgb(var(--color-primary-700) / <alpha-value>)',
          800: 'rgb(var(--color-primary-800) / <alpha-value>)',
          900: 'rgb(var(--color-primary-900) / <alpha-value>)',
        },
        success: {
          500: '#10b981',
          600: '#059669',
        },
        warning: {
          500: '#f59e0b',
          600: '#d97706',
        },
        danger: {
          500: '#ef4444',
          600: '#dc2626',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
```

**IMPORTANTE**: Las clases `primary-*` son DINAMICAS (via CSS var). Las clases `success-*`, `warning-*`, `danger-*` son ESTATICAS (colores fijos).

---

## 6. ARCHIVO 2: src/index.css

**Ruta**: `frontend/src/index.css`

Este es el archivo MAS CRITICO. Contiene los 6 temas, los componentes base y TODOS los overrides para dark mode. **Copiar COMPLETO tal cual**:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@layer base {
  :root {
    --color-primary-50: 227 242 253;
    --color-primary-100: 187 222 251;
    --color-primary-200: 144 202 249;
    --color-primary-300: 100 181 246;
    --color-primary-400: 66 165 245;
    --color-primary-500: 26 115 232;
    --color-primary-600: 21 101 192;
    --color-primary-700: 13 71 161;
    --color-primary-800: 10 61 145;
    --color-primary-900: 6 50 112;
    --surface-base: 249 250 251;
    --surface-card: 255 255 255;
    --surface-soft: 249 250 251;
    --border-subtle: 229 231 235;
    --border-strong: 209 213 219;
    --text-strong: 17 24 39;
    --text-default: 55 65 81;
    --text-muted: 107 114 128;
  }

  [data-theme='dark'] {
    --color-primary-50: 14 28 42;
    --color-primary-100: 20 46 67;
    --color-primary-200: 14 116 144;
    --color-primary-300: 6 182 212;
    --color-primary-400: 34 211 238;
    --color-primary-500: 34 211 238;
    --color-primary-600: 8 145 178;
    --color-primary-700: 14 116 144;
    --color-primary-800: 21 94 117;
    --color-primary-900: 8 47 73;
    --surface-base: 10 15 25;
    --surface-card: 17 24 39;
    --surface-soft: 30 41 59;
    --border-subtle: 51 65 85;
    --border-strong: 71 85 105;
    --text-strong: 241 245 249;
    --text-default: 203 213 225;
    --text-muted: 148 163 184;
  }

  [data-theme='grayscale'] {
    --color-primary-50: 245 245 245;
    --color-primary-100: 229 229 229;
    --color-primary-200: 212 212 212;
    --color-primary-300: 163 163 163;
    --color-primary-400: 115 115 115;
    --color-primary-500: 82 82 82;
    --color-primary-600: 64 64 64;
    --color-primary-700: 38 38 38;
    --color-primary-800: 24 24 27;
    --color-primary-900: 9 9 11;
    --surface-base: 244 244 245;
    --surface-card: 255 255 255;
    --surface-soft: 228 228 231;
    --border-subtle: 212 212 216;
    --border-strong: 161 161 170;
    --text-strong: 24 24 27;
    --text-default: 63 63 70;
    --text-muted: 113 113 122;
  }

  [data-theme='neon'] {
    --color-primary-50: 20 10 35;
    --color-primary-100: 45 20 75;
    --color-primary-200: 91 33 182;
    --color-primary-300: 168 85 247;
    --color-primary-400: 232 121 249;
    --color-primary-500: 217 70 239;
    --color-primary-600: 192 38 211;
    --color-primary-700: 8 247 254;
    --color-primary-800: 34 211 238;
    --color-primary-900: 103 232 249;
    --surface-base: 6 9 23;
    --surface-card: 17 24 39;
    --surface-soft: 24 24 27;
    --border-subtle: 91 33 182;
    --border-strong: 217 70 239;
    --text-strong: 250 245 255;
    --text-default: 224 231 255;
    --text-muted: 196 181 253;
  }

  [data-theme='pink'] {
    --color-primary-50: 255 241 242;
    --color-primary-100: 255 228 230;
    --color-primary-200: 254 205 211;
    --color-primary-300: 253 164 175;
    --color-primary-400: 251 113 133;
    --color-primary-500: 244 63 94;
    --color-primary-600: 225 29 72;
    --color-primary-700: 190 24 93;
    --color-primary-800: 157 23 77;
    --color-primary-900: 136 19 55;
    --surface-base: 255 247 248;
    --surface-card: 255 255 255;
    --surface-soft: 255 241 242;
    --border-subtle: 253 205 211;
    --border-strong: 251 113 133;
    --text-strong: 80 7 36;
    --text-default: 136 19 55;
    --text-muted: 159 18 57;
  }

  [data-theme='purple'] {
    --color-primary-50: 245 243 255;
    --color-primary-100: 237 233 254;
    --color-primary-200: 221 214 254;
    --color-primary-300: 196 181 253;
    --color-primary-400: 167 139 250;
    --color-primary-500: 124 58 237;
    --color-primary-600: 109 40 217;
    --color-primary-700: 91 33 182;
    --color-primary-800: 76 29 149;
    --color-primary-900: 59 7 100;
    --surface-base: 248 247 255;
    --surface-card: 255 255 255;
    --surface-soft: 245 243 255;
    --border-subtle: 221 214 254;
    --border-strong: 167 139 250;
    --text-strong: 46 16 101;
    --text-default: 88 28 135;
    --text-muted: 109 40 217;
  }

  * {
    border-color: rgb(var(--border-subtle));
  }

  html,
  body,
  #root {
    @apply h-full;
  }

  body {
    background-color: rgb(var(--surface-base));
    color: rgb(var(--text-strong));
    font-family: 'Inter', system-ui, -apple-system, sans-serif;
    @apply antialiased transition-colors duration-300;
  }
}

@layer components {
  .btn {
    @apply inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:pointer-events-none disabled:opacity-50;
  }

  .btn-primary {
    @apply bg-primary-500 text-white hover:bg-primary-600 focus:ring-primary-500;
  }

  .btn-secondary {
    background-color: rgb(var(--surface-soft));
    color: rgb(var(--text-default));
    @apply focus:ring-gray-500;
  }

  .btn-success {
    @apply bg-success-500 text-white hover:bg-success-600 focus:ring-success-500;
  }

  .btn-danger {
    @apply bg-danger-500 text-white hover:bg-danger-600 focus:ring-danger-500;
  }

  .input {
    background-color: rgb(var(--surface-card));
    border-color: rgb(var(--border-strong));
    color: rgb(var(--text-strong));
    @apply block w-full rounded-lg border px-3 py-2 text-sm placeholder:text-gray-400 transition-colors duration-300 focus:border-primary-500 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-0 disabled:opacity-50;
  }

  .card {
    background-color: rgb(var(--surface-card));
    border-color: rgb(var(--border-subtle));
    @apply rounded-lg border p-6 shadow-sm transition-colors duration-300;
  }

  .badge {
    @apply inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium;
  }

  .badge-success {
    @apply bg-green-100 text-green-800;
  }

  .badge-warning {
    @apply bg-yellow-100 text-yellow-800;
  }

  .badge-danger {
    @apply bg-red-100 text-red-800;
  }

  .badge-info {
    @apply bg-blue-100 text-blue-800;
  }
}

@layer utilities {
  .scrollbar-subtle {
    scrollbar-width: thin;
    scrollbar-color: rgba(255, 255, 255, 0.45) transparent;
  }

  .scrollbar-subtle::-webkit-scrollbar {
    width: 8px;
  }

  .scrollbar-subtle::-webkit-scrollbar-track {
    background: transparent;
  }

  .scrollbar-subtle::-webkit-scrollbar-thumb {
    background: rgba(255, 255, 255, 0.4);
    border-radius: 9999px;
    border: 2px solid transparent;
    background-clip: padding-box;
  }

  .scrollbar-subtle::-webkit-scrollbar-thumb:hover {
    background: rgba(255, 255, 255, 0.55);
    border: 2px solid transparent;
    background-clip: padding-box;
  }

  [data-theme='light'] .scrollbar-subtle,
  [data-theme='pink'] .scrollbar-subtle,
  [data-theme='purple'] .scrollbar-subtle,
  [data-theme='grayscale'] .scrollbar-subtle {
    scrollbar-color: rgba(148, 163, 184, 0.55) transparent;
  }

  [data-theme='light'] .scrollbar-subtle::-webkit-scrollbar-thumb,
  [data-theme='pink'] .scrollbar-subtle::-webkit-scrollbar-thumb,
  [data-theme='purple'] .scrollbar-subtle::-webkit-scrollbar-thumb,
  [data-theme='grayscale'] .scrollbar-subtle::-webkit-scrollbar-thumb {
    background: rgba(148, 163, 184, 0.55);
    border: 2px solid transparent;
    background-clip: padding-box;
  }

  [data-theme='light'] .scrollbar-subtle::-webkit-scrollbar-thumb:hover,
  [data-theme='pink'] .scrollbar-subtle::-webkit-scrollbar-thumb:hover,
  [data-theme='purple'] .scrollbar-subtle::-webkit-scrollbar-thumb:hover,
  [data-theme='grayscale'] .scrollbar-subtle::-webkit-scrollbar-thumb:hover {
    background: rgba(100, 116, 139, 0.65);
    border: 2px solid transparent;
    background-clip: padding-box;
  }

  [data-theme] .bg-white {
    background-color: rgb(var(--surface-card)) !important;
  }

  [data-theme] .bg-gray-50,
  [data-theme] .bg-gray-100 {
    background-color: rgb(var(--surface-soft)) !important;
  }

  [data-theme] .text-gray-900 {
    color: rgb(var(--text-strong)) !important;
  }

  [data-theme] .text-gray-700 {
    color: rgb(var(--text-default)) !important;
  }

  [data-theme] .text-gray-500,
  [data-theme] .text-gray-400 {
    color: rgb(var(--text-muted)) !important;
  }

  [data-theme] .border-gray-200 {
    border-color: rgb(var(--border-subtle)) !important;
  }

  [data-theme] .border-gray-300 {
    border-color: rgb(var(--border-strong)) !important;
  }

  [data-theme] .text-gray-600 {
    color: rgb(var(--text-muted)) !important;
  }

  [data-theme] .text-gray-800 {
    color: rgb(var(--text-strong)) !important;
  }

  [data-theme] .bg-gray-200 {
    background-color: rgb(var(--surface-soft)) !important;
  }

  [data-theme] .bg-gray-300 {
    background-color: rgb(var(--border-subtle)) !important;
  }

  [data-theme] .hover\:bg-gray-50:hover,
  [data-theme] .hover\:bg-gray-100:hover {
    background-color: rgb(var(--surface-soft)) !important;
  }

  [data-theme] .divide-gray-200 > :not([hidden]) ~ :not([hidden]) {
    border-color: rgb(var(--border-subtle)) !important;
  }

  [data-theme] .ring-gray-200 {
    --tw-ring-color: rgb(var(--border-subtle)) !important;
  }

  [data-theme] .ring-gray-300 {
    --tw-ring-color: rgb(var(--border-strong)) !important;
  }

  [data-theme] .placeholder\:text-gray-400::placeholder {
    color: rgb(var(--text-muted)) !important;
  }

  /* ───── Dark themes: tooltip / bg-gray-900 ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-gray-900 {
    background-color: rgb(var(--border-strong)) !important;
  }

  /* ───── Dark themes: semantic GREEN ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-green-50,
  :is([data-theme='dark'], [data-theme='neon']) .bg-green-50\/50 {
    background-color: rgba(16, 185, 129, 0.1) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .bg-green-100 {
    background-color: rgba(16, 185, 129, 0.18) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .text-green-600,
  :is([data-theme='dark'], [data-theme='neon']) .text-green-700,
  :is([data-theme='dark'], [data-theme='neon']) .text-green-800 {
    color: #6ee7b7 !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .border-green-200 {
    border-color: rgba(16, 185, 129, 0.3) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .hover\:bg-green-50:hover {
    background-color: rgba(16, 185, 129, 0.15) !important;
  }

  /* ───── Dark themes: semantic YELLOW ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-yellow-50,
  :is([data-theme='dark'], [data-theme='neon']) .bg-yellow-50\/50 {
    background-color: rgba(245, 158, 11, 0.1) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .bg-yellow-100 {
    background-color: rgba(245, 158, 11, 0.18) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .text-yellow-600,
  :is([data-theme='dark'], [data-theme='neon']) .text-yellow-700,
  :is([data-theme='dark'], [data-theme='neon']) .text-yellow-800 {
    color: #fcd34d !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .border-yellow-200 {
    border-color: rgba(245, 158, 11, 0.3) !important;
  }

  /* ───── Dark themes: semantic RED ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-red-50,
  :is([data-theme='dark'], [data-theme='neon']) .bg-red-50\/50 {
    background-color: rgba(239, 68, 68, 0.1) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .bg-red-100 {
    background-color: rgba(239, 68, 68, 0.18) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .text-red-600,
  :is([data-theme='dark'], [data-theme='neon']) .text-red-700,
  :is([data-theme='dark'], [data-theme='neon']) .text-red-800 {
    color: #fca5a5 !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .border-red-200 {
    border-color: rgba(239, 68, 68, 0.3) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .hover\:bg-red-50:hover {
    background-color: rgba(239, 68, 68, 0.15) !important;
  }

  /* ───── Dark themes: semantic BLUE ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-blue-50,
  :is([data-theme='dark'], [data-theme='neon']) .bg-blue-50\/50 {
    background-color: rgba(59, 130, 246, 0.1) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .bg-blue-100 {
    background-color: rgba(59, 130, 246, 0.18) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .text-blue-600,
  :is([data-theme='dark'], [data-theme='neon']) .text-blue-700,
  :is([data-theme='dark'], [data-theme='neon']) .text-blue-800 {
    color: #93c5fd !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .border-blue-200 {
    border-color: rgba(59, 130, 246, 0.3) !important;
  }

  /* ───── Dark themes: semantic ORANGE ───── */
  :is([data-theme='dark'], [data-theme='neon']) .bg-orange-50 {
    background-color: rgba(249, 115, 22, 0.1) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .bg-orange-100 {
    background-color: rgba(249, 115, 22, 0.18) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .text-orange-600,
  :is([data-theme='dark'], [data-theme='neon']) .text-orange-700,
  :is([data-theme='dark'], [data-theme='neon']) .text-orange-800 {
    color: #fdba74 !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .border-orange-200 {
    border-color: rgba(249, 115, 22, 0.3) !important;
  }

  /* ───── Dark themes: shadows → subtle border replacement ───── */
  :is([data-theme='dark'], [data-theme='neon']) .shadow-sm {
    box-shadow: 0 0 0 1px rgb(var(--border-subtle)), 0 1px 2px 0 rgba(0,0,0,.3) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .shadow-md {
    box-shadow: 0 0 0 1px rgb(var(--border-subtle)), 0 2px 6px rgba(0,0,0,.35) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .shadow-lg {
    box-shadow: 0 0 0 1px rgb(var(--border-subtle)), 0 4px 12px rgba(0,0,0,.4) !important;
  }
  :is([data-theme='dark'], [data-theme='neon']) .shadow-2xl {
    box-shadow: 0 0 0 1px rgb(var(--border-strong)), 0 8px 24px rgba(0,0,0,.5) !important;
  }

  .scrollbar-hide {
    -ms-overflow-style: none;
    scrollbar-width: none;
  }

  .scrollbar-hide::-webkit-scrollbar {
    display: none;
  }
}
```

---

## 7. ARCHIVO 3: src/stores/uiStore.js

**Ruta**: `frontend/src/stores/uiStore.js`

Este es el store global Zustand. Persiste el tema en `localStorage` bajo la key `ui-storage`.

```js
/**
 * Store de UI
 * Estado global para preferencias visuales y branding
 * Persistir tema, sidebar y datos visuales de la empresa
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

const useUiStore = create(
  persist(
    (set) => ({
      theme: 'light',
      sidebarCollapsed: false,
      companyName: 'Walos',
      companyLogoUrl: null,

      setTheme: (theme) => set({ theme }),
      setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
      setBranding: ({ companyName, companyLogoUrl }) =>
        set((state) => ({
          companyName: companyName === undefined ? state.companyName : companyName,
          companyLogoUrl: companyLogoUrl === undefined ? state.companyLogoUrl : companyLogoUrl,
        })),
    }),
    {
      name: 'ui-storage',
      partialize: (state) => ({
        theme: state.theme,
        sidebarCollapsed: state.sidebarCollapsed,
        companyName: state.companyName,
        companyLogoUrl: state.companyLogoUrl,
      }),
    }
  )
);

export default useUiStore;
```

**Clave**: `setTheme(theme)` actualiza el store → `ThemeSync` lo detecta → aplica `data-theme`.

---

## 8. ARCHIVO 4: ThemeSync en App.jsx

**Ruta**: `frontend/src/App.jsx`

Dentro del componente raiz `App`, agregar este componente **ANTES** del `<BrowserRouter>`:

```jsx
import useUiStore from './stores/uiStore';

const ThemeSync = () => {
  const theme = useUiStore((state) => state.theme);

  React.useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme || 'light');
  }, [theme]);

  return null;
};

// Dentro de App:
function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeSync />           {/* <── AQUI, ANTES de BrowserRouter */}
      <BrowserRouter>
        {/* ...rutas... */}
      </BrowserRouter>
    </QueryClientProvider>
  );
}
```

**POR QUE FUNCIONA**: `ThemeSync` se suscribe al store Zustand. Cada vez que `theme` cambia, el `useEffect` se ejecuta y pone el atributo `data-theme` en `<html>`. El CSS en `index.css` reacciona automaticamente.

---

## 9. ARCHIVO 5: ThemeSelector.jsx

**Ruta**: `frontend/src/modules/settings/components/ThemeSelector.jsx`

Este es el componente visual que muestra los 6 temas como tarjetas seleccionables:

```jsx
/**
 * Selector de Temas
 * Galeria de temas visuales disponibles
 * Permitir cambiar rapidamente la identidad cromatica de la app
 */

import { Check } from 'lucide-react';

const THEMES = [
  {
    id: 'light',
    name: 'Claro',
    description: 'Limpio y luminoso para operacion diaria.',
    preview: ['bg-white', 'bg-blue-100', 'bg-gray-100'],
  },
  {
    id: 'dark',
    name: 'Oscuro',
    description: 'Superficies profundas con alto contraste.',
    preview: ['bg-slate-900', 'bg-slate-700', 'bg-cyan-400'],
  },
  {
    id: 'grayscale',
    name: 'Escala de grises',
    description: 'Monocromatico y sobrio.',
    preview: ['bg-zinc-50', 'bg-zinc-400', 'bg-zinc-800'],
  },
  {
    id: 'neon',
    name: 'Neon',
    description: 'Oscuro con acentos intensos y vibrantes.',
    preview: ['bg-slate-950', 'bg-fuchsia-500', 'bg-cyan-400'],
  },
  {
    id: 'pink',
    name: 'Pink',
    description: 'Calido, expresivo y moderno.',
    preview: ['bg-rose-50', 'bg-rose-300', 'bg-pink-600'],
  },
  {
    id: 'purple',
    name: 'Morado',
    description: 'Profundo y elegante con tono premium.',
    preview: ['bg-violet-50', 'bg-violet-300', 'bg-violet-700'],
  },
];

const ThemeSelector = ({ selectedTheme, onSelect }) => {
  return (
    <section className="card space-y-6">
      <div>
        <h2 className="text-lg font-bold text-gray-900">Temas visuales</h2>
        <p className="mt-1 text-sm text-gray-500">
          Cambia la personalidad de toda la interfaz y revisa el resultado al instante.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {THEMES.map((theme) => {
          const isActive = selectedTheme === theme.id;

          return (
            <button
              key={theme.id}
              type="button"
              onClick={() => onSelect(theme.id)}
              className={`relative overflow-hidden rounded-xl border p-4 text-left transition-all duration-200 ${
                isActive
                  ? 'border-primary-500 ring-2 ring-primary-200 shadow-md'
                  : 'border-gray-200 hover:border-gray-300 hover:shadow-sm'
              }`}
            >
              {isActive && (
                <span className="absolute right-3 top-3 flex h-6 w-6 items-center justify-center rounded-full bg-primary-600 text-white shadow-sm">
                  <Check className="h-3.5 w-3.5" />
                </span>
              )}

              <div className="mb-4 flex gap-2">
                {theme.preview.map((colorClass) => (
                  <span
                    key={`${theme.id}-${colorClass}`}
                    className={`h-12 flex-1 rounded-lg border border-black/5 ${colorClass}`}
                  />
                ))}
              </div>

              <p className="text-sm font-semibold text-gray-900">{theme.name}</p>
              <p className="mt-1 text-sm text-gray-500">{theme.description}</p>
            </button>
          );
        })}
      </div>
    </section>
  );
};

export default ThemeSelector;
```

**Props**:
- `selectedTheme` (string): ID del tema activo (ej: `'dark'`)
- `onSelect` (function): Callback que recibe el ID del tema seleccionado

**Cada tarjeta muestra**:
- 3 barras de color como preview (usando clases Tailwind directas para el preview visual)
- Check icon si esta activo
- Nombre y descripcion
- Borde y ring `primary-*` si activo

---

## 10. ARCHIVO 6: Integracion en la pagina de Settings

En la pagina donde se muestren los Settings, la integracion del tema es asi:

### Estado local
```jsx
const [form, setForm] = useState({
  // ...otros campos...
  themePreference: 'light',
});
```

### Importar store
```jsx
import useUiStore from '../../stores/uiStore';
// Dentro del componente:
const { setTheme } = useUiStore();
```

### Handler de seleccion (cambio visual INMEDIATO)
```jsx
const handleThemeSelect = (theme) => {
  setForm((prev) => ({ ...prev, themePreference: theme }));
  setTheme(theme);  // ← ESTO hace el cambio visual instantaneo
};
```

### Renderizar el selector
```jsx
<ThemeSelector
  selectedTheme={form.themePreference}
  onSelect={handleThemeSelect}
/>
```

### Guardar en backend (al hacer clic en boton "Guardar")
```jsx
const handleSave = async () => {
  await companyService.updateSettings({
    displayName: form.displayName,
    email: form.email,
    phone: form.phone,
    themePreference: form.themePreference,  // ← ENVIA el tema al backend
  });
};
```

### Cargar tema desde backend (al montar la pagina)
```jsx
useEffect(() => {
  const settings = settingsData?.data;
  if (!settings) return;
  setForm(prev => ({ ...prev, themePreference: settings.themePreference || 'light' }));
  setTheme(settings.themePreference || 'light');
}, [settingsData, setTheme]);
```

---

## 11. ARCHIVO 7: Persistencia en Backend

### API Endpoint

```
PUT /api/v1/company/settings
Content-Type: application/json
Authorization: Bearer <jwt>

{
  "displayName": "Mi Negocio",
  "email": "info@negocio.com",
  "phone": "+57300123456",
  "themePreference": "dark"      ← ESTE CAMPO
}
```

### Controller (.NET)

```csharp
private static readonly HashSet<string> AllowedThemes = new(StringComparer.OrdinalIgnoreCase)
{
    "light", "dark", "grayscale", "neon", "pink", "purple"
};

[HttpPut("settings")]
public async Task<IActionResult> UpdateSettings([FromBody] UpdateCompanySettingsRequest request)
{
    // ...validaciones...

    if (!AllowedThemes.Contains(request.ThemePreference))
        return BadRequest(ApiResponse.Fail("Tema no permitido"));

    settings.ThemePreference = request.ThemePreference.Trim().ToLowerInvariant();
    // ...guardar...
}
```

### Campo en BD
La tabla `core.companies` (o `core.company_settings`) debe tener:
```sql
theme_preference VARCHAR(20) NOT NULL DEFAULT 'light'
```

### Servicio frontend (`companyService.js`)
```js
import api from '../config/api';

export const companyService = {
  getSettings: async () => {
    const response = await api.get('/company/settings');
    return response.data;
  },
  updateSettings: async (settingsData) => {
    const response = await api.put('/company/settings', settingsData);
    return response.data;
  },
};

export default companyService;
```

---

## 12. ARCHIVO 8: Layout.jsx (carga inicial del tema)

Cuando la app carga por primera vez, `Layout.jsx` obtiene los settings del servidor y aplica el tema:

```jsx
import useUiStore from '../../stores/uiStore';
import companyService from '../../services/companyService';

const Layout = ({ children }) => {
  const setTheme = useUiStore((state) => state.setTheme);

  const { data: settingsData } = useQuery({
    queryKey: ['company-settings'],
    queryFn: () => companyService.getSettings(),
    enabled: !!user,
    staleTime: 5 * 60 * 1000,
  });

  useEffect(() => {
    const settings = settingsData?.data;
    if (!settings) return;

    if (settings.themePreference) {
      setTheme(settings.themePreference);  // ← Aplica el tema guardado
    }
  }, [setTheme, settingsData]);

  // ...resto del layout...
};
```

**SECUENCIA COMPLETA al cargar la app**:
1. `uiStore` carga tema de `localStorage` (persist middleware)
2. `ThemeSync` aplica ese tema inmediatamente
3. `Layout` hace GET `/company/settings` → si hay `themePreference` en BD → `setTheme()` lo actualiza
4. `ThemeSync` detecta el cambio → actualiza `data-theme`

---

## 13. Reglas que NO debes romper

1. **NUNCA** usar colores hardcoded para fondos principales. Siempre usar `bg-white`, `bg-gray-50`, `text-gray-900` etc. — los overrides CSS se encargan de adaptarlos.

2. **SIEMPRE** usar `primary-*` para colores de accion (botones, links, focus rings). NUNCA `blue-500` directo.

3. Los 6 IDs de tema son: `light`, `dark`, `grayscale`, `neon`, `pink`, `purple`. **NO inventar nuevos** sin agregar sus variables CSS, overrides y validacion backend.

4. El atributo es `data-theme` en `<html>`, **NO** una clase CSS.

5. Los valores de variables CSS son `R G B` con espacios: `26 115 232`, **NO** `rgb(26, 115, 232)` ni `#1A73E8`.

6. Componentes reutilizables (`.card`, `.input`, `.btn-*`) ya usan variables CSS. **NO** sobreescribirlos con colores estaticos.

7. Para temas oscuros (`dark`, `neon`), los colores semanticos (green, yellow, red, blue, orange) **DEBEN** tener overrides con `:is([data-theme='dark'], [data-theme='neon'])`. Sin esto, un badge verde se vera invisible sobre fondo oscuro.

8. Las sombras en dark mode se reemplazan por `box-shadow` con borde sutil. **NO** dejar `shadow-sm` sin override en dark.

9. El tema se persiste en DOS lugares:
   - `localStorage` via Zustand persist → carga rapida al abrir la app
   - BD via `PUT /company/settings` → persistencia real compartida entre sesiones/usuarios

10. **`transition-colors duration-300`** en `body` y `.card` — esto hace que el cambio de tema sea suave, NO abrupto.

---

## 14. Checklist de verificacion

Despues de implementar, verificar CADA uno de estos puntos:

- [ ] Seleccionar cada tema → el cambio es visual INSTANTANEO (sin recargar)
- [ ] `body` cambia de fondo al cambiar tema
- [ ] Cards (`.card`) cambian de fondo y borde
- [ ] Inputs (`.input`) tienen fondo y texto correcto en cada tema
- [ ] `text-gray-900` se ve legible en TODOS los temas (incluido dark)
- [ ] `bg-white` se convierte en superficie oscura en dark/neon
- [ ] Badges de color (green, yellow, red) son legibles en dark/neon
- [ ] Sombras se ven bien en dark (borde sutil en vez de sombra negra sobre negro)
- [ ] Boton `primary-600` se ve bien en todos los temas
- [ ] Al guardar y recargar la pagina, el tema persiste
- [ ] Al abrir en otra pestana, el tema se carga correctamente (localStorage)
- [ ] Los colores del `preview` en ThemeSelector se ven correctamente (son hardcoded Tailwind, NO variables)
- [ ] El check icon aparece SOLO en el tema activo
- [ ] El borde/ring `primary-*` del tema activo se adapta al color del tema actual

---

## 15. SIDEBAR / LAYOUT COMPLETO

El sidebar y el header viven en un solo componente `Layout.jsx`. Se debe replicar **EXACTO** para que la experiencia sea identica.

### 15.1 Estructura visual

```
┌──────────────────────────────────────────────────────────────┐
│ SIDEBAR (aside)                  │ CONTENIDO PRINCIPAL       │
│ ┌──────────────────────────────┐ │ ┌───────────────────────┐ │
│ │ LOGO + boton colapsar       │ │ │ HEADER                │ │
│ │ (h-16, border-b)            │ │ │ logo+nombre │ bell+out│ │
│ ├──────────────────────────────┤ │ ├───────────────────────┤ │
│ │ NAV ITEMS                   │ │ │                       │ │
│ │ · Dashboard                 │ │ │  children (pagina)    │ │
│ │ · Asistente IA              │ │ │                       │ │
│ │ · Inventario                │ │ │                       │ │
│ │ · Ventas                    │ │ │                       │ │
│ │ · Finanzas                  │ │ │                       │ │
│ │ · Proveedores               │ │ │                       │ │
│ │ · Usuarios                  │ │ │                       │ │
│ │ ▼ Configuracion             │ │ │                       │ │
│ │   · Branding                │ │ │                       │ │
│ │   · Temas                   │ │ │                       │ │
│ │   · Descuentos              │ │ │                       │ │
│ ├──────────────────────────────┤ │ │                       │ │
│ │ USUARIO                     │ │ │                       │ │
│ │ avatar + nombre + email     │ │ │                       │ │
│ ├──────────────────────────────┤ │ │                       │ │
│ │ FOOTER                      │ │ │                       │ │
│ │ logo Walos centrado         │ │ │                       │ │
│ └──────────────────────────────┘ │ └───────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

### 15.2 Comportamientos clave

| Comportamiento | Detalle |
|---|---|
| **Ancho expandido** | `256px` (fijo) |
| **Ancho colapsado** | `68px` (solo iconos) |
| **Colapsar** | Boton `ChevronsLeft` en desktop, solo visible en `lg:` |
| **Expandir** | Click en logo Walos cuando esta colapsado |
| **Mobile** | Sidebar oculto por defecto (`-translate-x-full`). Se abre con boton hamburguesa (`Menu` icon). Overlay negro semi-transparente detras. |
| **Tooltip colapsado** | Cuando colapsado, hover en item muestra tooltip con nombre (position absolute, left 100%) |
| **Sub-items** | Solo se muestran si el item padre esta activo Y el sidebar NO esta colapsado |
| **Item activo** | `bg-primary-50 text-primary-600 font-semibold` |
| **Item inactivo** | `text-gray-700 hover:bg-primary-50 hover:text-primary-600` |
| **Transicion** | `transition-[width] duration-300 ease-in-out` en el aside |
| **Textos al colapsar** | `lg:w-0 lg:overflow-hidden lg:opacity-0` con transicion |
| **Estado persistente** | `sidebarCollapsed` en Zustand `uiStore` (persiste en localStorage) |

### 15.3 Iconos necesarios (lucide-react)

```js
import {
  Bell,           // Header: alertas
  Bot,            // Menu: Asistente IA
  ChevronsLeft,   // Boton colapsar sidebar
  ChevronsRight,  // (reservado, no usado actualmente)
  LayoutDashboard, // Menu: Dashboard
  Landmark,       // Menu: Finanzas
  LogOut,         // Header: cerrar sesion
  Menu,           // Header mobile: abrir sidebar
  Package,        // Menu: Inventario
  Palette,        // Submenu: Temas
  Percent,        // Submenu: Descuentos
  Settings,       // Menu: Configuracion (padre)
  ShoppingCart,   // Menu: Ventas
  Store,          // Submenu: Branding
  Truck,          // Menu: Proveedores
  Users,          // Menu: Usuarios
  X,              // Mobile: cerrar sidebar
} from 'lucide-react';
```

### 15.4 Definicion de menu

```js
const menuItems = [
  { name: 'Dashboard', path: '/', icon: LayoutDashboard },
  { name: 'Asistente IA', path: '/ai-assistant', icon: Bot },
  { name: 'Inventario', path: '/inventory', icon: Package },
  { name: 'Ventas', path: '/sales', icon: ShoppingCart },
  { name: 'Finanzas', path: '/finance', icon: Landmark },
  { name: 'Proveedores', path: '/suppliers', icon: Truck },
  { name: 'Usuarios', path: '/users', icon: Users },
  {
    name: 'Configuracion',
    path: '/settings',
    icon: Settings,
    children: [
      { name: 'Branding', path: '/settings/branding', icon: Store },
      { name: 'Temas', path: '/settings/themes', icon: Palette },
      { name: 'Descuentos', path: '/settings/discounts', icon: Percent },
    ],
  },
];
```

**Regla de "activo"**: Para Dashboard (`/`) se usa `pathname === '/'`. Para el resto `pathname.startsWith(item.path)`.

### 15.5 Codigo completo: Layout.jsx

**Ruta**: `frontend/src/components/layout/Layout.jsx`

```jsx
import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Bell,
  Bot,
  ChevronsLeft,
  ChevronsRight,
  LayoutDashboard,
  Landmark,
  LogOut,
  Menu,
  Package,
  Palette,
  Settings,
  ShoppingCart,
  Store,
  Percent,
  Truck,
  Users,
  X,
} from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import companyService from '../../services/companyService';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import useUiStore from '../../stores/uiStore';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';

const Layout = ({ children }) => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { user, logout, branchId } = useAuthStore();
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const setSidebarCollapsed = useUiStore((state) => state.setSidebarCollapsed);
  const companyName = useUiStore((state) => state.companyName);
  const companyLogoUrl = useUiStore((state) => state.companyLogoUrl);
  const setBranding = useUiStore((state) => state.setBranding);
  const setTheme = useUiStore((state) => state.setTheme);
  const navigate = useNavigate();
  const location = useLocation();

  // ── Cargar settings de empresa (incluye themePreference) ──
  const { data: settingsData } = useQuery({
    queryKey: ['company-settings'],
    queryFn: () => companyService.getSettings(),
    enabled: !!user,
    staleTime: 5 * 60 * 1000,
  });

  // ── Cargar alertas para badge en header ──
  const { data: alertsData } = useQuery({
    queryKey: ['alerts', branchId],
    queryFn: () => inventoryService.getAlerts(branchId),
    enabled: !!branchId && !!user,
    refetchInterval: 30000,
  });

  // ── Aplicar branding y tema desde BD ──
  useEffect(() => {
    const settings = settingsData?.data;
    if (!settings) return;

    setBranding({
      companyName: settings.displayName || settings.name || 'Walos',
      companyLogoUrl: settings.logoUrl || null,
    });

    if (settings.themePreference) {
      setTheme(settings.themePreference);
    }
  }, [setBranding, setTheme, settingsData]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  // ── Menu items ──
  const menuItems = [
    { name: 'Dashboard', path: '/', icon: LayoutDashboard },
    { name: 'Asistente IA', path: '/ai-assistant', icon: Bot },
    { name: 'Inventario', path: '/inventory', icon: Package },
    { name: 'Ventas', path: '/sales', icon: ShoppingCart },
    { name: 'Finanzas', path: '/finance', icon: Landmark },
    { name: 'Proveedores', path: '/suppliers', icon: Truck },
    { name: 'Usuarios', path: '/users', icon: Users },
    {
      name: 'Configuracion',
      path: '/settings',
      icon: Settings,
      children: [
        { name: 'Branding', path: '/settings/branding', icon: Store },
        { name: 'Temas', path: '/settings/themes', icon: Palette },
        { name: 'Descuentos', path: '/settings/discounts', icon: Percent },
      ],
    },
  ];

  // ── Variables derivadas ──
  const displayName = companyName || 'Walos';
  const logoSrc = companyLogoUrl ? `${API_BASE}${companyLogoUrl}` : null;
  const walosLogoSrc = '/walos-logo.png';
  const alertsCount = alertsData?.count || alertsData?.data?.length || 0;
  const userDisplayName = user?.first_name
    ? `${user.first_name} ${user?.last_name || ''}`.trim()
    : user?.name || 'Usuario';
  const userInitial = userDisplayName.charAt(0).toUpperCase();

  // ── Render de cada link del menu ──
  const renderMenuLink = (item, child = false) => {
    const Icon = item.icon;
    const isRoot = item.path === '/';
    const isActive = isRoot ? location.pathname === '/' : location.pathname.startsWith(item.path);

    return (
      <Link
        key={item.path}
        to={item.path}
        title={collapsed ? item.name : undefined}
        className={`group relative flex items-center gap-3 rounded-lg px-3 transition-colors ${
          child ? 'ml-3 border-l border-gray-200 py-2.5 pl-4 text-sm' : 'py-3'
        } ${
          isActive
            ? 'bg-primary-50 font-semibold text-primary-600'
            : 'text-gray-700 hover:bg-primary-50 hover:text-primary-600'
        }`}
        onClick={() => setSidebarOpen(false)}
      >
        <Icon className={`${child ? 'h-4 w-4' : 'h-5 w-5'} flex-shrink-0`} />
        <span
          className={`whitespace-nowrap font-medium transition-all duration-300 ${
            collapsed ? 'lg:w-0 lg:overflow-hidden lg:opacity-0' : 'opacity-100'
          }`}
        >
          {item.name}
        </span>

        {/* Tooltip visible solo cuando sidebar esta colapsado */}
        <span
          className={`
            pointer-events-none absolute left-full z-[60] ml-2 hidden whitespace-nowrap rounded-md bg-gray-900 px-2.5 py-1.5
            text-xs font-medium text-white shadow-lg transition-opacity duration-200
            ${collapsed ? 'lg:block opacity-0 group-hover:opacity-100' : ''}
          `}
        >
          {item.name}
        </span>
      </Link>
    );
  };

  return (
    <div className="flex h-full bg-gray-50">
      {/* ── Overlay mobile ── */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 transition-opacity duration-300 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* ══════════════════════════════════════════════ */}
      {/* ══  SIDEBAR                                 ══ */}
      {/* ══════════════════════════════════════════════ */}
      <aside
        style={{ width: sidebarOpen ? 256 : (collapsed ? 68 : 256) }}
        className={`
          fixed inset-y-0 left-0 z-50 flex flex-col overflow-hidden bg-white shadow-lg
          transition-[width] duration-300 ease-in-out
          lg:relative lg:translate-x-0
          ${sidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
        `}
      >
        {/* ── Cabecera sidebar: logo + boton colapsar ── */}
        <div className="flex h-16 items-center gap-3 border-b px-3">
          <div
            className={`flex min-w-0 items-center gap-3 overflow-hidden transition-all duration-300 ${
              collapsed ? 'w-0 opacity-0' : 'flex-1 opacity-100'
            }`}
          >
            <img
              src={walosLogoSrc}
              alt="Walos"
              className="h-10 w-10 flex-shrink-0 rounded-lg border border-gray-200 bg-white object-contain p-1"
            />
          </div>

          {collapsed ? (
            <button
              onClick={() => setSidebarCollapsed(false)}
              className="mx-auto hidden flex-shrink-0 items-center justify-center lg:flex"
              title="Abrir barra lateral"
            >
              <img
                src={walosLogoSrc}
                alt="Walos"
                className="h-10 w-10 rounded-lg object-contain"
              />
            </button>
          ) : (
            <button
              onClick={() => setSidebarCollapsed(true)}
              className="ml-auto hidden flex-shrink-0 items-center justify-center rounded-lg border border-gray-200 p-1.5 text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700 lg:flex"
              title="Cerrar barra lateral"
            >
              <ChevronsLeft className="h-4 w-4" />
            </button>
          )}

          {/* Boton cerrar solo en mobile */}
          <button onClick={() => setSidebarOpen(false)} className="ml-auto lg:hidden">
            <X className="h-6 w-6" />
          </button>
        </div>

        {/* ── Navegacion ── */}
        <nav className="flex-1 space-y-1 overflow-x-hidden overflow-y-auto p-2">
          {menuItems.map((item) => {
            const active = item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path);
            return (
              <div key={item.path} className="space-y-1">
                {renderMenuLink(item)}
                {!collapsed && active && item.children?.map((child) => renderMenuLink(child, true))}
              </div>
            );
          })}
        </nav>

        {/* ── Seccion usuario (abajo de nav) ── */}
        <div className="border-t p-2">
          <div className="flex items-center gap-3 rounded-lg bg-gray-50 p-2">
            {logoSrc ? (
              <img
                src={logoSrc}
                alt={displayName}
                className="h-10 w-10 flex-shrink-0 rounded-full border border-gray-200 object-cover"
                title={collapsed ? displayName : undefined}
              />
            ) : (
              <div
                className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-primary-500 font-semibold text-white"
                title={collapsed ? userDisplayName : undefined}
              >
                {userInitial}
              </div>
            )}
            <div
              className={`overflow-hidden transition-all duration-300 ${
                collapsed ? 'lg:w-0 lg:opacity-0' : 'flex-1 opacity-100'
              }`}
            >
              <p className="truncate text-sm font-medium">{userDisplayName}</p>
              <p className="truncate text-xs text-gray-500">{user?.email}</p>
            </div>
          </div>
        </div>

        {/* ── Footer: logo Walos ── */}
        <div className="border-t px-4 py-3">
          <div className="flex justify-center">
            <img
              src={walosLogoSrc}
              alt="Walos"
              className={`object-contain transition-all duration-300 ${
                collapsed ? 'h-6 w-6' : 'h-8 w-auto'
              }`}
            />
          </div>
        </div>
      </aside>

      {/* ══════════════════════════════════════════════ */}
      {/* ══  CONTENIDO PRINCIPAL                     ══ */}
      {/* ══════════════════════════════════════════════ */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* ── Header ── */}
        <header className="flex h-16 items-center justify-between border-b bg-white px-6 shadow-sm">
          <div className="flex min-w-0 items-center gap-3">
            {/* Hamburguesa mobile */}
            <button onClick={() => setSidebarOpen(true)} className="lg:hidden">
              <Menu className="h-6 w-6" />
            </button>

            <div className="flex min-w-0 items-center gap-3">
              {logoSrc ? (
                <img
                  src={logoSrc}
                  alt={displayName}
                  className="h-9 w-9 rounded-xl border border-gray-200 bg-white object-contain p-1"
                />
              ) : (
                <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary-100 text-sm font-bold text-primary-700">
                  {displayName.charAt(0).toUpperCase()}
                </div>
              )}

              <div className="min-w-0">
                <p className="truncate text-sm font-semibold text-gray-900">{displayName}</p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-4">
            {/* Boton alertas con badge */}
            <button
              onClick={() => navigate('/alerts')}
              className="relative rounded-lg p-2 transition-colors hover:bg-gray-100"
              title="Ver alertas"
            >
              <Bell className="h-5 w-5" />
              {alertsCount > 0 && (
                <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1.5 text-[10px] font-bold text-white">
                  {alertsCount > 99 ? '99+' : alertsCount}
                </span>
              )}
            </button>

            {/* Boton logout */}
            <button
              onClick={handleLogout}
              className="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-gray-100"
            >
              <LogOut className="h-5 w-5" />
              <span className="hidden sm:inline">Salir</span>
            </button>
          </div>
        </header>

        {/* ── Area de contenido ── */}
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
};

export default Layout;
```

### 15.6 Dependencias externas que usa Layout

| Import | Paquete | Para que |
|---|---|---|
| `Link, useLocation, useNavigate` | `react-router-dom` | Navegacion |
| `useQuery` | `@tanstack/react-query` | Cargar settings y alertas |
| Iconos | `lucide-react` | Ver lista en 15.3 |
| `companyService` | `../../services/companyService` | GET settings del servidor |
| `inventoryService` | `../../services/inventoryService` | GET alertas (para badge) |
| `useAuthStore` | `../../stores/authStore` | user, logout, branchId |
| `useUiStore` | `../../stores/uiStore` | theme, sidebar, branding |

### 15.7 Store necesario: uiStore.js (ya documentado en seccion 7)

El sidebar usa del store:
- `sidebarCollapsed` (boolean) — si esta colapsado o no
- `setSidebarCollapsed(bool)` — action para colapsar/expandir
- `companyName` — nombre a mostrar
- `companyLogoUrl` — logo de la empresa
- `setTheme(theme)` — para aplicar tema al cargar settings
- `setBranding({...})` — para aplicar nombre/logo al cargar settings

### 15.8 Store necesario: authStore.js

El layout necesita del auth store:
- `user` — objeto con `first_name`, `last_name`, `name`, `email`
- `logout()` — function para cerrar sesion
- `branchId` — branch activa del usuario
- `isAuthenticated` — para la ruta protegida

### 15.9 Detalles CSS criticos del sidebar

**El sidebar usa clases Tailwind estandar que YA se adaptan a todos los temas via los overrides de index.css:**

| Clase usada | En tema claro | En tema oscuro (via override) |
|---|---|---|
| `bg-white` | blanco | `var(--surface-card)` = slate-900 |
| `bg-gray-50` | gris claro | `var(--surface-soft)` = slate-800 |
| `text-gray-700` | gris oscuro | `var(--text-default)` = slate-300 |
| `text-gray-500` | gris medio | `var(--text-muted)` = slate-400 |
| `text-gray-900` | casi negro | `var(--text-strong)` = casi blanco |
| `border-gray-200` | borde gris | `var(--border-subtle)` = slate-700 |
| `bg-primary-50` | primary tint | Cambia segun tema |
| `text-primary-600` | primary | Cambia segun tema |
| `shadow-lg` | sombra normal | Borde sutil + sombra oscura |
| `hover:bg-gray-100` | hover gris | `var(--surface-soft)` |
| `hover:bg-primary-50` | hover primary | Cambia segun tema |

**NO es necesario agregar clases dark: al sidebar.** Todo funciona automaticamente por los overrides globales en `index.css`.

### 15.10 Responsive: breakpoints

| Breakpoint | Comportamiento |
|---|---|
| `< lg` (< 1024px) | Sidebar oculto. Se abre con hamburguesa. Overlay negro detras. Boton X para cerrar. |
| `>= lg` (1024px+) | Sidebar siempre visible. Se puede colapsar a 68px con ChevronsLeft. |

**Clases clave de responsive:**
- `lg:hidden` — hamburguesa solo en mobile
- `lg:relative lg:translate-x-0` — sidebar fijo en desktop
- `-translate-x-full lg:translate-x-0` — oculto en mobile, visible en desktop
- `hidden lg:flex` — boton colapsar solo en desktop
- `hidden sm:inline` — texto "Salir" oculto en pantallas muy pequenas

### 15.11 Animacion del colapso

El sidebar usa `transition-[width]` en vez de `transition-all` para que SOLO el ancho se anime (no otros props). Esto evita transiciones indeseadas en colores cuando cambia el tema.

Los textos del menu desaparecen con:
```
collapsed ? 'lg:w-0 lg:overflow-hidden lg:opacity-0' : 'opacity-100'
```
Esto los reduce a ancho 0 + oculta overflow + los hace invisibles, todo con transicion suave.

### 15.12 Sub-items (children)

Los children solo se muestran si:
1. El item padre esta **activo** (`pathname.startsWith(item.path)`)
2. El sidebar **NO** esta colapsado

```jsx
{!collapsed && active && item.children?.map((child) => renderMenuLink(child, true))}
```

Los sub-items tienen estilo diferente:
- `ml-3` — margen izquierdo para indentar
- `border-l border-gray-200` — linea vertical a la izquierda
- `py-2.5 pl-4 text-sm` — mas compactos y texto mas pequeno
- Iconos `h-4 w-4` en vez de `h-5 w-5`

---

## 16. Checklist de verificacion del Sidebar

- [ ] Sidebar expandido muestra logo + todos los items + usuario + footer
- [ ] Sidebar colapsado muestra solo iconos (68px de ancho)
- [ ] Click en logo colapsado → expande
- [ ] Click en ChevronsLeft → colapsa
- [ ] Hover en item colapsado → tooltip aparece a la derecha
- [ ] Item activo tiene `bg-primary-50 text-primary-600` (se adapta al tema)
- [ ] Sub-items de Configuracion aparecen solo cuando Configuracion esta activo
- [ ] Sub-items desaparecen cuando el sidebar esta colapsado
- [ ] En mobile (< 1024px): sidebar oculto, hamburguesa visible
- [ ] Click hamburguesa → sidebar se desliza, overlay negro aparece
- [ ] Click overlay o X → sidebar se cierra
- [ ] Header muestra logo empresa (o inicial), nombre, bell con badge, boton salir
- [ ] Badge de alertas muestra numero correcto (o "99+" si > 99)
- [ ] Todo el sidebar se adapta correctamente a los 6 temas (dark incluido)
- [ ] Estado colapsado persiste al recargar (localStorage via Zustand)
- [ ] Transicion de ancho es suave (300ms)
- [ ] Textos del menu desaparecen con fade al colapsar
