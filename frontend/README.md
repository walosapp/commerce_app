# Walos Frontend

PWA para gestión de bar y restaurante con asistencia de IA.

## 🚀 Tecnologías

- **Framework**: React 18
- **Build**: Vite
- **Styling**: TailwindCSS
- **State**: Zustand + React Query
- **Router**: React Router v6
- **Icons**: Lucide React
- **PWA**: vite-plugin-pwa
- **Voice**: Web Speech API

## 📦 Instalación

```bash
cd frontend
npm install
```

## ⚙️ Configuración

```bash
cp .env.example .env
# Editar .env con la URL del backend
```

### Descarga de Walos Agent

Configurá `VITE_WALOS_AGENT_DOWNLOAD_URL` en cada entorno con la URL HTTPS
estable del instalador `.exe`. Mientras no exista infraestructura definitiva,
la opción mínima es publicar el instalador como asset de una release del
repositorio y usar una URL estable como:

```text
https://github.com/walosapp/commerce_app/releases/latest/download/Walos-Agent-Setup.exe
```

La URL queda incorporada al build de Vite. Producción exige HTTPS; HTTP se
acepta únicamente para loopback durante desarrollo. Si falta o es insegura, la
interfaz mantiene la descarga deshabilitada y no redirige a una ubicación implícita.

## 🏃 Ejecución

```bash
# Desarrollo
npm run dev

# Build producción
npm run build

# Preview build
npm run preview
```

## 📱 Características PWA

- Instalable en dispositivos móviles
- Funciona offline (caché de assets)
- Notificaciones push (próximamente)
- Optimizado para móvil (mobile-first)

## 🎯 Módulos Implementados

### ✅ Inventario
- Chat con IA para registro de pedidos
- Reconocimiento de voz (Web Speech API)
- Tabla de stock con filtros
- Alertas de stock bajo
- Reportes de ganancias

### ⏳ Próximamente
- Autenticación (Login/Register)
- Ventas (POS)
- Proveedores
- Reportes avanzados
- Dashboard con gráficas

## 🤖 Uso del Asistente de IA

El asistente de inventario acepta comandos como:

- "Me llegaron 24 cervezas Corona a 18 pesos cada una"
- "Recibí un pedido de 12 botellas de vino a 85 pesos"
- "¿Cuánto estoy ganando en cervezas?"
- "Muéstrame los productos con stock bajo"

## 📁 Estructura

```
src/
├── components/
│   └── layout/          # Layout principal
├── modules/
│   └── inventory/       # Módulo de inventario
│       ├── components/  # Componentes específicos
│       └── InventoryPage.jsx
├── services/            # API calls
├── stores/              # Zustand stores
├── hooks/               # Custom hooks
├── config/              # Configuración
└── App.jsx
```

## 🎨 Diseño

- Mobile-first responsive
- TailwindCSS para estilos
- Componentes reutilizables
- Sistema de diseño consistente

## 🔐 Autenticación (Temporal)

Por ahora, para probar la app sin módulo de auth:

```javascript
// En la consola del navegador
localStorage.setItem('token', 'tu-token-jwt');
localStorage.setItem('tenantId', '1');
localStorage.setItem('branchId', '1');
```

## 📊 Estado Global

- **Auth**: Zustand (persistido en localStorage)
- **Server State**: React Query (caché automático)
- **UI State**: React hooks locales

## 🌐 Internacionalización

Preparado para i18n (próximamente):
- Español (es)
- Inglés (en)
- Portugués (pt)
