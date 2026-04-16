# Frontend Tests (Vitest + React Testing Library)

## Estructura

```
src/test/
├── setup.js                 # Configuración global de tests (mocks)
├── utils/                   # Tests de utilidades
│   └── formatters.test.js
├── stores/                  # Tests de Zustand stores
│   └── authStore.test.js
├── modules/                 # Tests de componentes/pages
│   └── auth/
│       └── LoginPage.test.jsx
└── README.md               # Este archivo
```

## Comandos

```bash
# Ejecutar todos los tests
npm test

# Ejecutar con UI
npm run test:ui

# Ejecutar con cobertura
npm run test:coverage

# Ejecutar en modo watch
npx vitest
```

## Configuración

- **Framework**: Vitest (alternativa moderna a Jest)
- **Testing Library**: React Testing Library + Jest DOM matchers
- **Environment**: jsdom (simula DOM en Node.js)
- **Mocks**: vi (integrado en Vitest)

## Mocks Globales (setup.js)

- Zustand stores (`authStore`, `uiStore`)
- Axios (HTTP client)
- react-hot-toast (notificaciones)
- react-router-dom (navegación)

## Escribir Nuevos Tests

### Componente
```jsx
import { render, screen } from '@testing-library/react'
import MyComponent from '@/components/MyComponent'

describe('MyComponent', () => {
  it('renders correctly', () => {
    render(<MyComponent />)
    expect(screen.getByText('Hello')).toBeInTheDocument()
  })
})
```

### Store
```js
import { describe, it, expect } from 'vitest'

describe('myStore', () => {
  it('should update state', () => {
    // Test store logic
  })
})
```

### Utilidad
```js
import { describe, it, expect } from 'vitest'

describe('formatter', () => {
  it('formats currency', () => {
    expect(formatCurrency(1000)).toBe('$1,000.00')
  })
})
```
