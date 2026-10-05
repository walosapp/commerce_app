import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PosDeliPage from '../../../modules/pos-deli/PosDeliPage';

const mocks = vi.hoisted(() => ({
  getProducts: vi.fn(),
  getFavorites: vi.fn(),
  getActive: vi.fn(),
  addUnitItem: vi.fn(),
  auth: { tenantId: 155, branchId: 91, user: { role: 'cashier', isPlatformAdmin: false } },
}));

vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => ({ canAccess: () => true, hasFeature: () => true, isReady: true, isError: false }) }));
vi.mock('../../../services/cashRegisterService', () => ({ cashRegisterService: { getActive: mocks.getActive } }));

vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../services/posDeliService', () => ({
  default: {
    getProducts: mocks.getProducts,
    getFavorites: mocks.getFavorites,
    createSale: vi.fn(),
  },
}));
vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale: vi.fn() }),
}));
vi.mock('../../../modules/pos-deli/stores/posDeliStore', () => ({
  default: () => ({
    items: [],
    selectedItemId: null,
    getTotal: () => 0,
    addWeighedItem: vi.fn(),
    addUnitItem: mocks.addUnitItem,
    removeItem: vi.fn(),
    updateQuantity: vi.fn(),
    setSelectedItemId: vi.fn(),
    clearTicket: vi.fn(),
    getIdempotencyKey: vi.fn(),
    renewIdempotencyKey: vi.fn(),
  }),
}));
vi.mock('../../../modules/pos-deli/hooks/useScale', () => ({
  default: () => ({ weight: 0, isStable: false, isConnected: false, error: null, config: null }),
}));
vi.mock('../../../modules/pos-deli/hooks/useBarcodeScanner', () => ({ default: vi.fn() }));
vi.mock('../../../modules/pos-deli/components/PaymentModal', () => ({ default: () => null }));
vi.mock('../../../modules/pos-deli/components/WeightInputModal', () => ({ default: () => null }));

describe('PosDeli catalog request', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.branchId = 91;
    mocks.auth.user = { role: 'cashier', isPlatformAdmin: false, name: 'Edwin', branchName: 'Principal' };
    mocks.getActive.mockResolvedValue({ data: { status: 'open', branchName: 'Principal' } });
    mocks.getProducts.mockResolvedValue({
      data: [{ id: 501, name: 'Café POS', salePrice: 5000, unitAbbreviation: 'und' }],
    });
    mocks.getFavorites.mockResolvedValue({ data: [] });
  });

  const renderPos = () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    return render(<QueryClientProvider client={client}><PosDeliPage /></QueryClientProvider>);
  };

  it('shows the operational header and three sections with an empty disabled sale', async () => {
    renderPos();
    expect(await screen.findByText('Caja: ABIERTA')).toBeInTheDocument();
    expect(screen.getByText('Edwin')).toBeInTheDocument();
    expect(screen.getByText('Principal')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Categorías de productos' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Catálogo de productos' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Venta actual' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'COBRAR $0' })).toBeDisabled();
  });

  it('places the only product search in the POS header, outside the catalog panel', async () => {
    renderPos();
    await screen.findByText('Caja: ABIERTA');
    const search = screen.getByRole('textbox', { name: 'Buscar producto' });
    expect(search.closest('header')).not.toBeNull();
    expect(within(screen.getByRole('region', { name: 'Catálogo de productos' })).queryByRole('textbox')).not.toBeInTheDocument();
    expect(search).toHaveAttribute('data-barcode-allowed', 'true');
    fireEvent.keyDown(window, { key: 'F1' });
    expect(search).toHaveFocus();
  });

  it('applies the existing subtle scrollbar only to the product list', async () => {
    renderPos();
    const product = await screen.findByRole('button', { name: /Café POS/ });
    const scrollArea = product.parentElement.parentElement;
    expect(scrollArea).toHaveClass('overflow-y-auto', 'scrollbar-subtle');
    expect(screen.getByRole('region', { name: 'Venta actual' }).querySelector('.scrollbar-subtle')).toBeNull();
  });

  it('filters categories and searches locally without losing the category list or returning favorites for no matches', async () => {
    mocks.getProducts.mockResolvedValue({ data: [
      { id: 1, name: 'Hamburguesa', salePrice: 18000, categoryName: 'Comidas', unitAbbreviation: 'und', sku: 'HAM-1' },
      { id: 2, name: 'Coca-Cola', salePrice: 5000, categoryName: 'Bebidas', unitAbbreviation: 'und', barcode: '770123' },
    ] });
    mocks.getFavorites.mockResolvedValue({ data: [{ id: 3, name: 'Favorito', unitAbbreviation: 'und' }] });
    renderPos();
    await screen.findByText('Hamburguesa');
    const nav = screen.getByRole('navigation');
    fireEvent.click(within(nav).getByRole('button', { name: 'Bebidas' }));
    expect(screen.queryByText('Hamburguesa')).not.toBeInTheDocument();
    expect(screen.getByText('Coca-Cola')).toBeInTheDocument();
    fireEvent.click(within(nav).getByRole('button', { name: 'Todos' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Buscar producto' }), { target: { value: 'HAM-1' } });
    expect(screen.getByText('Hamburguesa')).toBeInTheDocument();
    expect(screen.queryByText('Coca-Cola')).not.toBeInTheDocument();
    expect(within(nav).getByRole('button', { name: 'Bebidas' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: 'Buscar producto' }), { target: { value: '770123' } });
    expect(screen.getByText('Coca-Cola')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('textbox', { name: 'Buscar producto' }), { target: { value: 'inexistente' } });
    expect(screen.getByText('No hay productos para mostrar')).toBeInTheDocument();
    expect(screen.queryByText('Favorito')).not.toBeInTheDocument();
    expect(mocks.getProducts).toHaveBeenCalledTimes(1);
  });

  it('adds a unit product from its tile and keeps F1 search focus', async () => {
    renderPos();
    fireEvent.click(await screen.findByRole('button', { name: /POS.*\$/ }));
    expect(mocks.addUnitItem).toHaveBeenCalledWith(expect.objectContaining({ id: 501, isWeighed: false }));
    fireEvent.keyDown(window, { key: 'F1' });
    expect(screen.getByRole('textbox', { name: 'Buscar producto' })).toHaveFocus();
  });

  it('does not load products or cash without a branch and exposes the context problem', async () => {
    mocks.auth.branchId = null;
    renderPos();
    expect(screen.getByText('Necesitás una sucursal asignada para cargar productos.')).toBeInTheDocument();
    expect(mocks.getProducts).not.toHaveBeenCalled();
    expect(mocks.getActive).not.toHaveBeenCalled();
  });

  it('does not resurrect favorites after a successful empty catalog response', async () => {
    mocks.getProducts.mockResolvedValue({ data: [] });
    mocks.getFavorites.mockResolvedValue({ data: [{ id: 9, name: 'Favorito desactualizado', unitAbbreviation: 'und' }] });
    renderPos();
    await screen.findByText('No hay productos para mostrar');
    expect(screen.queryByText('Favorito desactualizado')).not.toBeInTheDocument();
  });

  it('keeps the scale inline with catalog metadata instead of a separate card', async () => {
    mocks.getProducts.mockResolvedValue({ data: [{ id: 8, name: 'Queso', salePrice: 20000, unitAbbreviation: 'kg' }] });
    renderPos();
    const scale = await screen.findByLabelText('Estado de la báscula');
    expect(scale.parentElement).toHaveTextContent('1 productos');
    expect(scale.parentElement).toHaveTextContent('F1 · Buscar');
    expect(scale).toHaveTextContent('0.000 kg');
    expect(scale).toHaveTextContent('Desconectada');
    expect(scale.querySelector('p')).toBeNull();
  });

  it('shows a product load error and retries the existing read endpoint', async () => {
    mocks.getProducts.mockRejectedValueOnce(new Error('offline'));
    renderPos();
    expect(await screen.findByText('No se pudieron cargar los productos.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar catálogo' }));
    await screen.findByText(/POS$/, { selector: 'span' });
    expect(mocks.getProducts).toHaveBeenCalledTimes(2);
  });

  it('loads products for a cashier with valid tenant and branch context', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <PosDeliPage />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(mocks.getProducts).toHaveBeenCalledWith({ search: '' }));
    expect(await screen.findByText('Café POS')).toBeInTheDocument();
  });
});
