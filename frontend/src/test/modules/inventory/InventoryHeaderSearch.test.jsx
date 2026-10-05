import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import InventoryPage from '../../../modules/inventory/InventoryPage';

const mocks = vi.hoisted(() => ({ auth: {}, getStock: vi.fn(), getLowStock: vi.fn(), getAlerts: vi.fn() }));
vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../services/inventoryService', () => ({ default: mocks }));
vi.mock('../../../modules/inventory/components/ProductFormModal', () => ({ default: ({ isOpen }) => isOpen && <div role="dialog">Crear producto</div> }));
vi.mock('../../../modules/inventory/components/ImportProductsModal', () => ({ default: ({ isOpen }) => isOpen && <div role="dialog">Importar productos</div> }));
vi.mock('../../../modules/inventory/components/DeleteConfirmModal', () => ({ default: () => null }));
vi.mock('../../../modules/inventory/components/AddStockModal', () => ({ default: () => null }));

const stock = [
  { productId: 1, productName: 'Coca Cola', sku: 'BEB-001', category: 'Bebidas', productType: 'simple', stockStatus: 'ok', quantity: 12 },
  { productId: 2, productName: 'Papa cruda', sku: 'INS-002', category: 'Vegetales', productType: 'supply', stockStatus: 'low', quantity: 2 },
  { productId: 3, productName: 'Hamburguesa', sku: 'COM-003', category: 'Comidas', productType: 'prepared', stockStatus: 'out', quantity: 0 },
];
const clients = [];
function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  clients.push(client);
  return { client, ...render(<QueryClientProvider client={client}><InventoryPage /></QueryClientProvider>) };
}
const search = () => screen.getByRole('searchbox', { name: 'Buscar en inventario' });
beforeEach(() => {
  vi.clearAllMocks();
  mocks.auth = { tenantId: 3, branchId: 7, user: { role: 'manager', name: 'Edwin Campo', branchName: 'Principal', isPlatformAdmin: false } };
  mocks.getStock.mockResolvedValue({ data: stock, count: stock.length });
  mocks.getLowStock.mockResolvedValue({ data: [stock[1]], count: 1 });
  mocks.getAlerts.mockResolvedValue({ data: [], count: 0 });
});
afterEach(() => clients.splice(0).forEach(client => client.clear()));

describe('Inventario: encabezado estilo POS con buscador', () => {
  it('reserves remaining space for a keyboard-accessible table with a fixed footer', async () => {
    const { container } = mount();
    const table = await screen.findByRole('table');
    const viewport = screen.getByRole('region', { name: 'Tabla de inventario' });
    expect(viewport).toHaveAttribute('tabindex', '0');
    expect(viewport).toHaveClass('inventory-table-scroll', 'overflow-auto', 'min-h-0', 'flex-1');
    expect(within(viewport).getByRole('table')).toBe(table);
    expect(container.firstElementChild).toHaveClass('flex', 'flex-1', 'min-h-0');
    const footer = screen.getByText('Mostrando 3 de 3 productos');
    expect(footer).toHaveClass('shrink-0');
    expect(viewport.contains(footer)).toBe(false);
  });

  it('overlays filters without reducing table space and closes on Escape or outside click', async () => {
    mount();
    await screen.findByRole('table');
    const trigger = screen.getByRole('button', { name: 'Filtros' });
    fireEvent.click(trigger);
    expect(screen.getByRole('region', { name: 'Filtros de inventario' })).toHaveClass('absolute', 'top-full');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(trigger).toHaveFocus();
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    fireEvent.click(trigger);
    fireEvent.pointerDown(search());
    expect(screen.queryByRole('region', { name: 'Filtros de inventario' })).not.toBeInTheDocument();
  });

  it('ubica un único buscador en el encabezado, con operador/sucursal/hora y sin datos de caja', async () => {
    const { client, container } = mount();
    await screen.findByRole('table');
    const header = container.querySelector('header');
    expect(header).toHaveClass('xl:grid-cols-[minmax(0,1fr)_minmax(240px,1.25fr)_minmax(0,1fr)]');
    expect(within(header).getByRole('heading', { name: 'Inventario' })).toBeInTheDocument();
    expect(within(header).getByRole('searchbox', { name: 'Buscar en inventario' })).toBe(search());
    expect(screen.getAllByPlaceholderText('Buscar por nombre, SKU o categoría...')).toHaveLength(1);
    expect(within(screen.getByRole('table').parentElement.parentElement).queryByRole('searchbox')).not.toBeInTheDocument();
    expect(within(header).getByText('Edwin Campo')).toBeInTheDocument();
    expect(within(header).getByText('Principal')).toBeInTheDocument();
    expect(header.querySelector('time')).toHaveTextContent(/\d{2}:\d{2}/);
    expect(screen.queryByText(/Caja:/)).not.toBeInTheDocument();
    expect(client.getQueryCache().getAll().map(q => q.queryKey)).toEqual([['stock', 7], ['lowStock', 7], ['alerts', 7]]);
  });

  it.each(['cOcA', 'beb-001', 'bebidas'])('mantiene búsqueda por nombre/SKU/categoría sin distinguir mayúsculas: %s', async (term) => {
    mount();
    await screen.findByText('Coca Cola');
    fireEvent.change(search(), { target: { value: term } });
    expect(screen.getByText('Coca Cola')).toBeInTheDocument();
    expect(screen.queryByText('Papa cruda')).not.toBeInTheDocument();
    expect(screen.queryByText('Hamburguesa')).not.toBeInTheDocument();
    expect(screen.getByText('Mostrando 1 de 3 productos')).toBeInTheDocument();
    expect(mocks.getStock).toHaveBeenCalledOnce();
  });

  it('combina buscador con filtros de stock/tipo y conserva los conteos generales', async () => {
    mount();
    await screen.findByText('Papa cruda');
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }));
    fireEvent.click(screen.getByRole('button', { name: /Stock Bajo/ }));
    fireEvent.click(screen.getByRole('button', { name: /Insumos/ }));
    fireEvent.change(search(), { target: { value: 'INS' } });
    expect(screen.getByText('Papa cruda')).toBeInTheDocument();
    expect(screen.queryByText('Coca Cola')).not.toBeInTheDocument();
    expect(screen.getByText('Total Productos').parentElement).toHaveTextContent('3');
    fireEvent.change(search(), { target: { value: 'Coca' } });
    expect(screen.getByText('No se encontraron productos')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Stock Bajo/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Quitar filtro' }));
    expect(screen.getByText('Coca Cola')).toBeInTheDocument();
    expect(search()).toHaveValue('Coca');
  });

  it('muestra el resumen compacto sin filtros visibles ni selección inicial', async () => {
    mount();
    await screen.findByText('Coca Cola');
    const summary = screen.getByRole('region', { name: 'Resumen de inventario' });
    expect(summary.querySelectorAll('dt')).toHaveLength(3);
    expect(screen.queryByRole('button', { name: 'Stock Bajo' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Insumos/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Filtros de inventario' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Filtros' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('Mostrando 3 de 3 productos')).toBeInTheDocument();
  });

  it('abrir filtros no modifica resultados hasta elegir una opción', async () => {
    mount();
    await screen.findByText('Coca Cola');
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }));
    expect(screen.getByRole('region', { name: 'Filtros de inventario' })).toBeInTheDocument();
    expect(screen.getByText('Mostrando 3 de 3 productos')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Todos' })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Sin Stock' }));
    expect(screen.getByText('Hamburguesa')).toBeInTheDocument();
    expect(screen.queryByText('Coca Cola')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sin Stock' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('conserva filtros al ocultar el panel e indica los activos; limpiar no borra el buscador', async () => {
    mount();
    await screen.findByText('Papa cruda');
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }));
    fireEvent.click(screen.getByRole('button', { name: 'Stock Bajo' }));
    fireEvent.click(screen.getByRole('button', { name: /Insumos/ }));
    expect(screen.getByRole('button', { name: 'Filtros 2' })).toHaveAttribute('aria-expanded', 'true');
    fireEvent.click(screen.getByRole('button', { name: 'Filtros 2' }));
    expect(screen.queryByRole('region', { name: 'Filtros de inventario' })).not.toBeInTheDocument();
    expect(screen.getByText('Papa cruda')).toBeInTheDocument();
    expect(screen.queryByText('Coca Cola')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Filtros 2' })).toHaveAttribute('aria-expanded', 'false');
    fireEvent.change(search(), { target: { value: 'Coca' } });
    expect(screen.getByText('No se encontraron productos')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Limpiar filtros' }));
    expect(search()).toHaveValue('Coca');
    expect(screen.getByText('Coca Cola')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Filtros' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Limpiar filtros' })).not.toBeInTheDocument();
  });

  it('permite limpiar búsqueda y conserva ordenamiento en la tabla', async () => {
    mount();
    await screen.findByText('Coca Cola');
    fireEvent.change(search(), { target: { value: 'inexistente' } });
    expect(screen.getByText('Intenta con otro término de búsqueda')).toBeInTheDocument();
    fireEvent.change(search(), { target: { value: '' } });
    fireEvent.click(screen.getByRole('columnheader', { name: 'Físico' }));
    const rows = within(screen.getByRole('table')).getAllByRole('row').slice(1);
    expect(rows[0]).toHaveTextContent('Hamburguesa');
    expect(rows[1]).toHaveTextContent('Papa cruda');
    expect(rows[2]).toHaveTextContent('Coca Cola');
  });

  it('deja el buscador disponible durante la carga y aplica lo escrito al recibir stock', async () => {
    let resolveStock;
    mocks.getStock.mockReturnValue(new Promise(resolve => { resolveStock = resolve; }));
    mount();
    expect(screen.getByText('Cargando stock...')).toBeInTheDocument();
    fireEvent.change(search(), { target: { value: 'Coca' } });
    resolveStock({ data: stock, count: 3 });
    expect(await screen.findByText('Coca Cola')).toBeInTheDocument();
    expect(screen.queryByText('Hamburguesa')).not.toBeInTheDocument();
  });

  it('conserva acciones de importar/crear en encabezado para gerente', async () => {
    const { container } = mount();
    const header = within(container.querySelector('header'));
    fireEvent.click(header.getByRole('button', { name: 'Importar Excel' }));
    expect(screen.getByRole('dialog')).toHaveTextContent('Importar productos');
    fireEvent.click(header.getByRole('button', { name: 'Agregar Producto' }));
    expect(screen.getAllByRole('dialog')).toHaveLength(2);
    await waitFor(() => expect(mocks.getStock).toHaveBeenCalledWith(7));
  });

  it('no amplía permisos de escritura para cajero', async () => {
    mocks.auth.user.role = 'cashier';
    mount();
    expect(await screen.findByText('Coca Cola')).toBeInTheDocument();
    expect(search()).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Agregar Producto' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Importar Excel' })).not.toBeInTheDocument();
    expect(screen.queryByTitle('Editar producto')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Eliminar producto')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Agregar stock')).not.toBeInTheDocument();
  });
});
