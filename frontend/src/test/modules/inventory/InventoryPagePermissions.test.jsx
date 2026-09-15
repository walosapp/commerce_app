import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import InventoryPage from '../../../modules/inventory/InventoryPage';

const { authState, queryConfigs, stockProps } = vi.hoisted(() => ({
  authState: { branchId: 7, user: { role: 'cashier', isPlatformAdmin: false } },
  queryConfigs: [],
  stockProps: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  useQuery: (config) => {
    queryConfigs.push(config);
    return { data: { data: [], count: 0 }, isLoading: false };
  },
}));
vi.mock('../../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../../modules/inventory/components/StockTable', () => ({
  default: (props) => { stockProps.push(props); return <div>Inventario lectura</div>; },
}));
vi.mock('../../../modules/inventory/components/ProductFormModal', () => ({ default: () => <div>Formulario producto</div> }));
vi.mock('../../../modules/inventory/components/DeleteConfirmModal', () => ({ default: () => <div>Eliminar producto</div> }));
vi.mock('../../../modules/inventory/components/AddStockModal', () => ({ default: () => <div>Agregar stock modal</div> }));
vi.mock('../../../modules/inventory/components/ImportProductsModal', () => ({ default: () => <div>Importar productos modal</div> }));

describe('InventoryPage InventoryWrite policy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
    stockProps.length = 0;
  });

  it('preserves inventory reads but removes every write action for cashier', () => {
    authState.user = { role: 'cashier', isPlatformAdmin: false };

    render(<InventoryPage />);

    expect(screen.getByText('Inventario lectura')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Agregar Producto/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Importar Excel/i })).not.toBeInTheDocument();
    expect(screen.queryByText('Formulario producto')).not.toBeInTheDocument();
    expect(stockProps.at(-1)).toEqual(expect.objectContaining({ onEdit: undefined, onDelete: undefined, onAddStock: undefined }));
    expect(queryConfigs.every((query) => query.enabled === true)).toBe(true);
  });

  it('keeps InventoryWrite actions for a manager', () => {
    authState.user = { role: 'manager', isPlatformAdmin: false };

    render(<InventoryPage />);

    expect(screen.getByRole('button', { name: /Agregar Producto/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Importar Excel/i })).toBeInTheDocument();
    expect(stockProps.at(-1).onEdit).toEqual(expect.any(Function));
  });
});
