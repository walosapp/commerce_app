# Walos — Guía de Estilo UI/UX

> **OBLIGATORIO**: Todo agente de IA o desarrollador que trabaje en este proyecto DEBE seguir esta guía sin excepción.
> Cualquier componente, página o módulo nuevo debe respetar estos patrones exactamente.
> Si un patrón no está documentado aquí, tomar como referencia los módulos de Inventario y Ventas ya implementados.

---

## 1. Stack Tecnológico Frontend

| Concepto | Tecnología |
|----------|-----------|
| Framework | React 18 |
| Bundler | Vite |
| Estilos | TailwindCSS (utility-first) |
| Íconos | Lucide React (`lucide-react`) — **NO usar emojis** |
| Estado global | Zustand (`stores/`) |
| Estado servidor | React Query (`@tanstack/react-query`) |
| HTTP | Axios (instancia en `config/api.js`) |
| Notificaciones | `react-hot-toast` |
| Routing | React Router v6 |
| Formularios | React controlled components (`useState`) |

### Stack Backend

| Concepto | Tecnología |
|----------|-----------|
| Framework | ASP.NET Core 8 |
| ORM | Dapper (raw SQL) |
| DB | SQL Server (`SCM_App_Track_Me`) |
| Auth | JWT |
| Arquitectura | Clean Architecture (API → Application → Domain ← Infrastructure) |

---

## 2. Estructura de Archivos

```
frontend/src/
├── components/layout/    # Layout, Header, Sidebar
├── config/               # api.js (axios instance)
├── modules/
│   ├── inventory/        # InventoryPage.jsx + components/
│   ├── sales/            # SalesPage.jsx + components/
│   ├── auth/             # LoginPage.jsx
│   └── [nuevo-modulo]/   # [Modulo]Page.jsx + components/
├── services/             # [modulo]Service.js (API calls)
├── stores/               # Zustand stores
└── utils/                # formatCurrency.js, helpers
```

**Regla**: Cada módulo tiene su carpeta en `modules/` con:
- `[Modulo]Page.jsx` — página principal
- `components/` — componentes internos del módulo
- Servicio correspondiente en `services/[modulo]Service.js`

---

## 3. Patrones de Componentes

### 3.1 Comentario de Cabecera (OBLIGATORIO en cada archivo)

```jsx
/**
 * Nombre del Componente
 * ¿Qué es? Descripción breve
 * ¿Para qué? Propósito funcional
 */
```

### 3.2 Página Principal (`[Modulo]Page.jsx`)

Estructura estándar:

```jsx
const ModuloPage = () => {
  // 1. Hooks de estado
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();

  // 2. Queries (React Query)
  const { data, isLoading } = useQuery({ ... });

  // 3. Handlers
  const handleAction = async () => { ... };

  // 4. Render
  return (
    <div className="flex flex-col h-[calc(100vh-7rem)] overflow-hidden">
      {/* Header: título + botón acción principal */}
      <div className="flex items-center justify-between mb-4 flex-shrink-0">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Título</h1>
          <p className="mt-1 text-sm text-gray-500">Subtítulo descriptivo</p>
        </div>
        <button className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-primary-700 transition-colors shadow-sm">
          <Icon className="h-4 w-4" />
          Acción Principal
        </button>
      </div>

      {/* Contenido principal */}
      <div className="flex-1 overflow-y-auto">
        {/* ... */}
      </div>

      {/* Modales/Paneles */}
    </div>
  );
};
```

**Altura del módulo**: `h-[calc(100vh-7rem)]` — esto descuenta el header (4rem) + padding del Layout (3rem).

---

## 4. Componentes de UI — Patrones Exactos

### 4.1 Botones

**Primario (acción principal)**:
```jsx
<button className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-primary-700 transition-colors shadow-sm">
  <Icon className="h-4 w-4" />
  Texto
</button>
```

**Secundario (acción alternativa)**:
```jsx
<button className="flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
  <Icon className="h-4 w-4" />
  Texto
</button>
```

**Peligro (eliminar/cancelar)**:
```jsx
<button className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors">
  <X className="h-4 w-4" />
</button>
```

**Éxito (facturar/confirmar)**:
```jsx
<button className="flex items-center gap-1.5 rounded-lg bg-green-600 px-3 py-2 text-xs font-medium text-white hover:bg-green-700 transition-colors">
  <Receipt className="h-3.5 w-3.5" />
  Facturar
</button>
```

**Reglas generales de botones**:
- Siempre `rounded-lg`
- Siempre `transition-colors`
- Ícono + texto: `gap-2`, ícono `h-4 w-4`
- Solo ícono: `p-2`
- Disabled: `disabled:opacity-50`
- Tamaño texto: `text-sm font-medium`

### 4.2 Inputs

```jsx
<input
  type="text"
  value={value}
  onChange={(e) => setValue(e.target.value)}
  placeholder="Placeholder..."
  className="input"
/>
```

La clase `input` está definida en `index.css`. Para inputs con ícono:

```jsx
<div className="relative">
  <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
  <input className="input pl-10" placeholder="Buscar..." />
</div>
```

### 4.3 Selects

```jsx
<select value={value} onChange={(e) => setValue(e.target.value)} className="input">
  <option value="">Seleccionar...</option>
  {options.map((opt) => (
    <option key={opt.id} value={opt.id}>{opt.name}</option>
  ))}
</select>
```

---

## 5. Modal — Patrón Estándar

```jsx
const MyModal = ({ isOpen, onClose, onSave, data }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="w-full max-w-lg max-h-[90vh] overflow-y-auto rounded-xl bg-white shadow-2xl m-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h2 className="text-lg font-bold text-gray-900">Título</h2>
          <button onClick={onClose} className="rounded-lg p-1.5 hover:bg-gray-100 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Body */}
        <div className="px-6 py-4 space-y-4">
          {/* Campos del formulario */}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 border-t px-6 py-4">
          <button onClick={onClose} className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
            Cancelar
          </button>
          <button onClick={handleSave} className="rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors">
            Guardar
          </button>
        </div>
      </div>
    </div>
  );
};
```

**Reglas de modales**:
- Overlay: `fixed inset-0 z-[70] bg-black/50`
- Contenedor: `max-w-lg max-h-[90vh] overflow-y-auto rounded-xl shadow-2xl m-4`
- Click en overlay cierra, click en contenido hace `stopPropagation()`
- Header con border-b, body con space-y-4, footer con border-t
- Padding horizontal: `px-6`, vertical: `py-4`

---

## 6. Panel Lateral (Slide-in) — Patrón Estándar

```jsx
const MyPanel = ({ isOpen, onClose, title = 'Título' }) => {
  return (
    <>
      {/* Overlay */}
      {isOpen && (
        <div className="fixed inset-0 z-[60] bg-black/40" onClick={onClose} />
      )}

      {/* Panel */}
      <div className={`fixed inset-y-0 right-0 z-[70] w-full max-w-lg bg-white shadow-2xl flex flex-col
        transition-transform duration-300 ease-in-out
        ${isOpen ? 'translate-x-0' : 'translate-x-full'}`}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b px-5 py-4 flex-shrink-0">
          <div>
            <h2 className="text-lg font-bold text-gray-900">{title}</h2>
            <p className="text-xs text-gray-500">Descripción breve</p>
          </div>
          <button onClick={onClose} className="rounded-lg p-1.5 hover:bg-gray-100 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Body (scrollable) */}
        <div className="flex-1 overflow-y-auto px-5 py-4">
          {/* Contenido */}
        </div>

        {/* Footer (fixed) */}
        <div className="border-t bg-gray-50 px-5 py-4 flex-shrink-0">
          {/* Resumen + botón acción */}
        </div>
      </div>
    </>
  );
};
```

**Reglas de paneles laterales**:
- Siempre desliza desde la derecha: `translate-x-full` → `translate-x-0`
- Transición: `duration-300 ease-in-out`
- Z-index: overlay `z-[60]`, panel `z-[70]`
- Ancho: `w-full max-w-lg`
- Header, body scrollable, footer fijo: flex-col con `flex-shrink-0` en header/footer

---

## 7. Formulario de Producto — Patrón de Referencia

### 7.1 Campos con Label

```jsx
<div>
  <label className="block text-sm font-medium text-gray-700 mb-1">Nombre del campo</label>
  <input className="input" value={value} onChange={...} />
</div>
```

### 7.2 Grid de 2 Columnas

```jsx
<div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">Campo 1</label>
    <input className="input" />
  </div>
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">Campo 2</label>
    <input className="input" />
  </div>
</div>
```

### 7.3 Cálculo en Tiempo Real (Margen ↔ Precio)

Cuando hay campos interdependientes (ej: costo, margen%, precio venta), se calculan mutuamente en `onChange`:

```jsx
const handleCostChange = (val) => {
  setCost(val);
  setSalePrice((val * (1 + margin / 100)).toFixed(2));
};

const handleMarginChange = (val) => {
  setMargin(val);
  setSalePrice((cost * (1 + val / 100)).toFixed(2));
};

const handlePriceChange = (val) => {
  setSalePrice(val);
  if (cost > 0) setMargin((((val - cost) / cost) * 100).toFixed(2));
};
```

---

## 8. Imagen Upload — Patrón Estándar

### 8.1 Zona de Upload

```jsx
<div
  onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
  onDragLeave={() => setDragOver(false)}
  onDrop={handleDrop}
  className={`relative flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-6
    transition-colors cursor-pointer
    ${dragOver ? 'border-primary-500 bg-primary-50' : 'border-gray-300 hover:border-gray-400'}`}
  onClick={() => fileInputRef.current?.click()}
>
  <Upload className="h-8 w-8 text-gray-400 mb-2" />
  <p className="text-sm text-gray-600">Arrastra una imagen o haz clic</p>
  <p className="text-xs text-gray-400 mt-1">JPG, PNG, WebP · Máx 2MB</p>
  <input ref={fileInputRef} type="file" accept="image/*" className="hidden" onChange={handleFileSelect} />
</div>
```

### 8.2 Preview de Imagen

```jsx
{imagePreview && (
  <div className="relative">
    <img src={imagePreview} className="h-32 w-32 rounded-lg object-cover border" />
    <button
      onClick={removeImage}
      className="absolute -right-2 -top-2 rounded-full bg-red-500 p-1 text-white shadow hover:bg-red-600"
    >
      <X className="h-3 w-3" />
    </button>
  </div>
)}
```

### 8.3 Soporte para Cámara Móvil

```jsx
<button className="...">
  <Camera className="h-4 w-4" />
  <input type="file" accept="image/*" capture="environment" className="hidden" />
</button>
```

**Reglas de upload**:
- Validación: JPG, PNG, WebP solamente. Max 2MB.
- Drag & drop con visual feedback (`border-primary-500 bg-primary-50`)
- Preview con botón de eliminar (X rojo flotante)
- Backend guarda en `wwwroot/uploads/[modulo]/`
- URL servida como archivo estático

---

## 9. Tablas — Patrón Estándar

### 9.1 Estructura

```jsx
<div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
  <table className="min-w-full divide-y divide-gray-200">
    <thead>
      <tr className="bg-gray-50">
        <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">
          Columna
        </th>
      </tr>
    </thead>
    <tbody className="divide-y divide-gray-100">
      {items.map((item) => (
        <tr key={item.id} className="hover:bg-gray-50 transition-colors">
          <td className="px-4 py-3 text-sm text-gray-700">{item.value}</td>
        </tr>
      ))}
    </tbody>
  </table>
</div>
```

### 9.2 Columna de Acciones

```jsx
<td className="px-4 py-3">
  <div className="flex items-center gap-1">
    <button onClick={...} className="rounded p-1.5 text-gray-400 hover:bg-blue-50 hover:text-blue-600 transition-colors" title="Editar">
      <Pencil className="h-4 w-4" />
    </button>
    <button onClick={...} className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 transition-colors" title="Eliminar">
      <Trash2 className="h-4 w-4" />
    </button>
  </div>
</td>
```

### 9.3 Thumbnail en Tabla

```jsx
<div className="flex items-center gap-3">
  {item.imageUrl ? (
    <img src={`${API_BASE}${item.imageUrl}`} className="h-10 w-10 rounded-lg object-cover border" />
  ) : (
    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-100">
      <ImageIcon className="h-5 w-5 text-gray-400" />
    </div>
  )}
  <span className="font-medium">{item.name}</span>
</div>
```

### 9.4 Badges de Estado

```jsx
// Ok
<span className="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-700">
  Óptimo
</span>

// Warning
<span className="inline-flex items-center rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-700">
  Stock bajo
</span>

// Error
<span className="inline-flex items-center rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-medium text-red-700">
  Sin stock
</span>
```

---

## 10. Stat Cards — Patrón Estándar

```jsx
<button
  onClick={() => handleFilter(stat.key)}
  className={`card text-left transition-all duration-200 ${
    isActive ? `ring-2 ${stat.ring} shadow-md` : 'hover:shadow-md'
  }`}
>
  <div className="flex items-center justify-between">
    <div>
      <p className="text-sm text-gray-500">{stat.label}</p>
      <p className="mt-2 text-3xl font-bold">{stat.value}</p>
      {isActive && (
        <p className="mt-1 text-xs text-gray-400">Clic para quitar filtro</p>
      )}
    </div>
    <div className={`rounded-lg p-3 ${stat.bgColor}`}>
      <Icon className={`h-6 w-6 ${stat.color}`} />
    </div>
  </div>
</button>
```

**Reglas de stat cards**:
- Son botones clickeables que filtran contenido
- Toggle: click activa, click de nuevo desactiva
- Activa: `ring-2` + color del tema + `shadow-md`
- Grid responsive: `grid gap-6 sm:grid-cols-2 lg:grid-cols-3`

---

## 11. Cards Arrastrables (Ventas)

### 11.1 Comportamiento Responsivo

- **Desktop (≥ 768px)**: Cards con posición absoluta, arrastrables con mouse
- **Mobile (< 768px)**: Cards en grid flow normal (1 col teléfono, 2 cols tablet), sin drag

### 11.2 Detección de Mobile

```jsx
const useIsMobile = () => {
  const [mobile, setMobile] = useState(window.innerWidth < 768);
  useEffect(() => {
    const handler = () => setMobile(window.innerWidth < 768);
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler);
  }, []);
  return mobile;
};
```

### 11.3 Card con +/- Inline

Cada item en una card de mesa tiene controles inline:

```jsx
<div className="flex items-center gap-1 flex-shrink-0">
  <button onClick={decrement} className="rounded p-0.5 text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors">
    <Minus className="h-3.5 w-3.5" />
  </button>
  <span className="w-6 text-center font-semibold text-gray-800 text-xs">{qty}</span>
  <button onClick={increment} className="rounded p-0.5 text-gray-400 hover:bg-green-50 hover:text-green-500 transition-colors">
    <Plus className="h-3.5 w-3.5" />
  </button>
</div>
```

---

## 12. Product Grid (Selección de Productos)

```jsx
<div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
  {products.map((product) => (
    <div
      className={`relative rounded-xl border-2 bg-white overflow-hidden transition-all cursor-pointer ${
        isSelected ? 'border-primary-500 shadow-md' : 'border-gray-200 hover:border-gray-300'
      }`}
      onClick={handleSelect}
    >
      {/* Badge de cantidad */}
      {qty > 0 && (
        <div className="absolute top-2 right-2 z-10 flex h-6 w-6 items-center justify-center rounded-full bg-primary-600 text-xs font-bold text-white shadow">
          {qty}
        </div>
      )}

      {/* Imagen */}
      <div className="aspect-square bg-gray-50 flex items-center justify-center overflow-hidden">
        <img or placeholder />
      </div>

      {/* Info */}
      <div className="p-2">
        <p className="text-xs font-medium text-gray-900 truncate">{name}</p>
        <p className="text-sm font-bold text-primary-600 mt-0.5">{price}</p>
      </div>

      {/* +/- controls (visible si seleccionado) */}
    </div>
  ))}
</div>
```

---

## 13. Servicios API — Patrón Estándar

```js
import api from '../config/api';

export const moduloService = {
  getAll: async (branchId) => {
    const response = await api.get('/modulo/endpoint', { params: { branchId } });
    return response.data;
  },

  create: async (data) => {
    const response = await api.post('/modulo/endpoint', data);
    return response.data;
  },

  update: async (id, data) => {
    const response = await api.put(`/modulo/endpoint/${id}`, data);
    return response.data;
  },

  delete: async (id) => {
    const response = await api.delete(`/modulo/endpoint/${id}`);
    return response.data;
  },

  // Upload con FormData
  uploadImage: async (id, file) => {
    const formData = new FormData();
    formData.append('image', file);
    const response = await api.post(`/modulo/endpoint/${id}/image`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },
};

export default moduloService;
```

---

## 14. React Query — Patrón Estándar

```jsx
const { data, isLoading } = useQuery({
  queryKey: ['nombre-recurso', branchId],
  queryFn: () => moduloService.getAll(branchId),
  enabled: !!branchId,
  refetchInterval: 30000, // solo si se necesita polling
});

// Invalidar cache después de mutaciones
const refetch = () => {
  queryClient.invalidateQueries({ queryKey: ['nombre-recurso'] });
};
```

---

## 15. Notificaciones Toast

```jsx
import toast from 'react-hot-toast';

toast.success('Acción completada');
toast.error('Error: descripción');
```

Configuración en `App.jsx`:
```jsx
<Toaster position="top-right" toastOptions={{
  duration: 3000,
  style: { borderRadius: '0.75rem', padding: '12px 16px' },
}} />
```

---

## 16. Formato de Moneda

Usar siempre `formatCurrency` de `utils/formatCurrency.js`:

```jsx
import { formatCurrency } from '../utils/formatCurrency';

<span>{formatCurrency(value)}</span>
```

**NUNCA** formatear moneda manualmente con template strings.

---

## 17. Colores del Tema

| Uso | Clase |
|-----|-------|
| Primario | `bg-primary-600`, `text-primary-600`, `hover:bg-primary-700` |
| Primario suave | `bg-primary-50`, `text-primary-600` |
| Éxito | `bg-green-600`, `text-green-700`, `bg-green-100` |
| Peligro | `bg-red-500`, `text-red-500`, `bg-red-50` |
| Warning | `bg-yellow-100`, `text-yellow-700` |
| Neutro | `bg-gray-50`, `text-gray-500`, `border-gray-200` |

**NUNCA** usar colores hex inline. Siempre clases Tailwind.

---

## 18. Íconos — Reglas

- **Librería**: Solo `lucide-react`
- **Tamaños**:
  - En botones con texto: `h-4 w-4`
  - Solo ícono (botón): `h-4 w-4` a `h-5 w-5`
  - En stat cards: `h-6 w-6`
  - Placeholders grandes: `h-8 w-8` a `h-16 w-16`
- **NUNCA usar emojis** en la interfaz

---

## 19. Responsividad

| Breakpoint | Uso |
|-----------|-----|
| Default | Mobile first (1 columna) |
| `sm:` (640px) | 2 columnas en grids |
| `md:` (768px) | Desktop — drag habilitado, sidebar visible |
| `lg:` (1024px) | 3+ columnas, sidebar siempre visible |

**Reglas**:
- Grid de forms: `grid-cols-1 sm:grid-cols-2`
- Grid de cards: `grid-cols-1 sm:grid-cols-2 lg:grid-cols-3`
- Paneles: `w-full max-w-lg` (se adapta solo)
- Botones de texto se ocultan en mobile: `<span className="hidden sm:inline">Texto</span>`

---

## 20. Z-Index — Convención

| Elemento | Z-Index |
|----------|---------|
| Cards normales | `z-10` |
| Cards arrastrando | `z-50` |
| Sidebar mobile | `z-50` |
| Overlay de panel/modal | `z-[60]` |
| Panel lateral / Modal | `z-[70]` |

---

## 21. Backend — Patrones

### 21.1 Respuestas API

Siempre usar `ApiResponse`:

```csharp
return Ok(ApiResponse<List<T>>.Ok(list, count: list.Count));
return Ok(ApiResponse.Ok("Mensaje de éxito"));
return BadRequest(ApiResponse.Fail("Mensaje de error"));
return NotFound(ApiResponse.Fail("No encontrado"));
```

### 21.2 Repository Pattern

```csharp
public async Task<IEnumerable<T>> GetAllAsync(long companyId, long branchId)
{
    try
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT ... FROM ... WHERE company_id = @CompanyId AND branch_id = @BranchId";
        return await connection.QueryAsync<T>(sql, new { CompanyId = companyId, BranchId = branchId });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error description");
        throw;
    }
}
```

### 21.3 Naming SQL

- Columnas SQL: `snake_case` (`company_id`, `created_at`)
- Alias Dapper: `PascalCase` (`AS CompanyId`, `AS CreatedAt`)
- Esquemas: `[core]`, `[inventory]`, `[sales]`, `[suppliers]`

### 21.4 Soft Delete

Nunca `DELETE FROM`. Siempre `UPDATE ... SET deleted_at = GETDATE()`. Todos los queries deben filtrar `AND deleted_at IS NULL`.

### 21.5 Multi-tenant

Toda query debe filtrar por `company_id`. Nunca devolver datos sin filtro de empresa.

---

## 22. Checklist para Nuevos Módulos

Antes de empezar cualquier módulo nuevo, verificar:

- [ ] Leer esta guía completa
- [ ] Crear carpeta `modules/[modulo]/` con `Page.jsx` + `components/`
- [ ] Crear `services/[modulo]Service.js`
- [ ] Agregar ruta en `App.jsx` con `<ProtectedRoute>`
- [ ] Verificar que el menú ya existe en `Layout.jsx` (sidebar)
- [ ] Scripts SQL en `backend-dotnet/sql/` con numeración secuencial
- [ ] Entidades en `Domain/Entities/`
- [ ] Interfaces en `Domain/Interfaces/`
- [ ] Repository en `Infrastructure/Repositories/`
- [ ] Registrar DI en `Infrastructure/DependencyInjection.cs`
- [ ] Controller en `API/Controllers/`
- [ ] DTOs en `Application/DTOs/[Modulo]/`
- [ ] Comentario de cabecera en cada archivo
- [ ] Mobile-first, responsivo
- [ ] Usar patrones de esta guía para modales, paneles, tablas, forms
- [ ] `formatCurrency` para moneda, Lucide para íconos, toast para notificaciones
