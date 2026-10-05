# Walos Frontend

React 18, Vite 5, TailwindCSS, Zustand, TanStack Query y React Router. Rutas y capacidades verificadas estáticamente el 2026-10-04; no se certifica funcionamiento end-to-end.

## Configuración e inicio

Desde `frontend`, con Node/npm compatibles con el lockfile:

```powershell
npm ci
npm run dev
```

Comandos documentados, no ejecutados en esta revisión. `VITE_API_URL` es el origen de la API (por defecto `http://localhost:3000`), **sin** `/api/v1`; `VITE_API_VERSION` vale `v1` por defecto. `src/config/api.js` agrega el prefijo. Vite escucha normalmente en 5173.

## Navegación y sesión

`src/App.jsx` define inventario, mesas, POS, caja, finanzas, proveedores/compras, delivery, usuarios, perfil, configuración y administración. No son pantallas futuras ni el login es temporal: las rutas consultan sesión, roles y funciones habilitadas. Axios adjunta el token Bearer; ante 401 limpia sesión y redirige a login. La autorización definitiva pertenece al backend.

## PWA y hardware

Manifest y Workbox están configurados. La API usa `NetworkOnly`: no se promete registrar ventas sin red.

La descarga de Walos Agent depende de `VITE_WALOS_AGENT_DOWNLOAD_URL`. El workflow [release-walos-agent.yml](../.github/workflows/release-walos-agent.yml) valida coincidencia de tag, versión del proyecto y URL configurada. Esa configuración no demuestra que el instalador publicado o desplegado haya pasado UAT. Ver [hardware POS](../docs/hardware-pos.md), conservado sin modificar.

## Scripts reales

| Comando | Alcance |
|---|---|
| `npm run dev` | Desarrollo Vite |
| `npm run lint` | ESLint con cero warnings |
| `npm run test -- --run` | Vitest una sola ejecución |
| `npm run test:coverage -- --run` | Vitest con cobertura |
| `npm run test:e2e` | Playwright |
| `npm run build` | Build de Vite |
| `npm run preview` | Sirve el build existente |

Playwright usa `E2E_BASE_URL` o `http://localhost:5173`; la configuración no arranca automáticamente servidores. Revisá datos y credenciales de pruebas antes de correrlo. **No se ejecutaron estos comandos en la revisión documental.**

[Guía de usuario](../docs/GUIA-USUARIO.md) · [Guía técnica](../docs/GUIA-TECNICA.md)
