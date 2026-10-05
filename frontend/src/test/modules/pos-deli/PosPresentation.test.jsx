import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import PosHeader from '../../../modules/pos-deli/components/PosHeader';
import TicketPanel from '../../../modules/pos-deli/components/TicketPanel';
import ProductGrid from '../../../modules/pos-deli/components/ProductGrid';
import ScaleIndicator from '../../../modules/pos-deli/components/ScaleIndicator';

const mocks = vi.hoisted(() => ({ auth: {}, features: {}, query: {}, config: null, getStatus: vi.fn() }));
vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => mocks.features }));
vi.mock('../../../services/cashRegisterService', () => ({ cashRegisterService: { getStatus: mocks.getStatus } }));
vi.mock('@tanstack/react-query', () => ({ useQuery: (config) => { mocks.config = config; return mocks.query; } }));

describe('POS header', () => {
  beforeEach(() => {
    mocks.auth = { isAuthenticated: true, tenantId: 155, branchId: 91, user: { name: 'Edwin', branchName: 'Principal' } };
    mocks.features = { isReady: true, isError: false, canAccess: () => true, hasFeature: () => true };
    mocks.query = { isSuccess: true, isError: false, data: { data: { branchId: 91, status: 'open' } }, refetch: vi.fn() };
  });

  it('reads shared active cash in the current context and shows the logged-in cashier, not the opener', async () => {
    mocks.query.data.data.openedByName = 'Otro operador';
    render(<PosHeader />);
    expect(screen.getByText('Caja: ABIERTA')).toBeInTheDocument();
    expect(screen.getByText('Edwin')).toBeInTheDocument();
    expect(screen.queryByText('Otro operador')).not.toBeInTheDocument();
    expect(mocks.config.queryKey).toEqual(['cash-register-status', 155, 91]);
    expect(mocks.config.enabled).toBe(true);
    await mocks.config.queryFn();
    expect(mocks.getStatus).toHaveBeenCalled();
  });

  it('shows closed only after a successful read without an active register', () => {
    mocks.query.data = { data: { branchId: 91, status: 'closed' } };
    render(<PosHeader />);
    expect(screen.getByText('Caja: CERRADA')).toBeInTheDocument();
  });

  it('does not show a stale open register as verified when the read fails and allows retry', () => {
    mocks.query.isError = true;
    render(<PosHeader />);
    expect(screen.getByText('Caja: NO SE PUDO VERIFICAR')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar caja' }));
    expect(mocks.query.refetch).toHaveBeenCalledOnce();
  });

  it('shows loading before a cash response', () => {
    mocks.query = { isSuccess: false, data: undefined };
    render(<PosHeader />);
    expect(screen.getByText('Caja: CARGANDO')).toBeInTheDocument();
  });

  it('never queries disabled cash features', () => {
    mocks.features.canAccess = (code) => code === 'pos';
    mocks.features.hasFeature = () => false;
    render(<PosHeader />);
    expect(mocks.config.enabled).toBe(false);
    expect(screen.getByText('Caja: NO HABILITADA')).toBeInTheDocument();
  });

  it('never queries cash for a role without access even with the feature enabled', () => {
    mocks.features.canAccess = () => false;
    render(<PosHeader />);
    expect(mocks.config.enabled).toBe(false);
    expect(screen.getByText('Caja: NO DISPONIBLE PARA TU ROL')).toBeInTheDocument();
  });

  it('does not invent a branch name when auth contains only its ID', () => {
    mocks.auth.user.branchName = null;
    render(<PosHeader />);
    expect(screen.getByText('#91')).toBeInTheDocument();
  });
});

describe('POS sale and product tiles', () => {
  const items = [{ id: '1', name: 'Hamburguesa', quantity: 2, unitPrice: 18000, subtotal: 36000, isWeighed: false }];
  const props = () => ({ items, total: 36000, selectedItemId: null, onSelectItem: vi.fn(), onRemoveItem: vi.fn(), onUpdateQuantity: vi.fn(), onCheckout: vi.fn() });

  it('shows the amount on checkout and preserves independent accessible item controls', () => {
    const callbacks = props();
    const { container } = render(<TicketPanel {...callbacks} />);
    expect(container.querySelector('button button')).toBeNull();
    expect(screen.getByText('Subtotal')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Sumar Hamburguesa' }));
    expect(callbacks.onUpdateQuantity).toHaveBeenCalledWith('1', 3);
    fireEvent.click(screen.getByRole('button', { name: 'Restar Hamburguesa' }));
    expect(callbacks.onUpdateQuantity).toHaveBeenCalledWith('1', 1);
    fireEvent.click(screen.getByRole('button', { name: /Hamburguesa 2 und/ }));
    expect(callbacks.onSelectItem).toHaveBeenCalledWith('1');
    fireEvent.click(screen.getByRole('button', { name: 'Quitar Hamburguesa' }));
    expect(callbacks.onRemoveItem).toHaveBeenCalledWith('1');
    fireEvent.click(screen.getByRole('button', { name: 'COBRAR $36.000' }));
    expect(callbacks.onCheckout).toHaveBeenCalledOnce();
  });

  it('disables ticket mutations and checkout while a sale is being submitted', () => {
    const callbacks = props();
    render(<TicketPanel {...callbacks} disabled />);
    for (const button of screen.getAllByRole('button')) expect(button).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'PROCESANDO…' }));
    expect(callbacks.onCheckout).not.toHaveBeenCalled();
  });

  it('retains weighed selection and per-unit price on a single product grid', () => {
    const onSelectWeighed = vi.fn();
    const onSelectUnit = vi.fn();
    const product = { id: 2, name: 'Queso', salePrice: 20000, unitAbbreviation: 'kg', isWeighed: true };
    render(<ProductGrid weighedProducts={[product]} unitProducts={[]} onSelectWeighed={onSelectWeighed} onSelectUnit={onSelectUnit} />);
    fireEvent.click(screen.getByRole('button', { name: /Queso.*20.000.*\/kg/ }));
    expect(onSelectWeighed).toHaveBeenCalledWith(product);
    expect(onSelectUnit).not.toHaveBeenCalled();
    expect(screen.queryByText('No hay productos por unidad para mostrar')).not.toBeInTheDocument();
  });

  it('resolves managed product image keys and falls back gracefully on image errors', () => {
    const product = { id: 42, name: 'Café', salePrice: 5000, imageUrl: 'companies/155/products/42/photo.webp' };
    const { container } = render(<ProductGrid weighedProducts={[]} unitProducts={[product]} onSelectUnit={vi.fn()} />);
    const image = container.querySelector('img');
    expect(image.src).toContain('/api/v1/media/image?key=companies%2F155%2Fproducts%2F42%2Fphoto.webp');
    fireEvent.error(image);
    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByRole('button', { name: /Café/ })).toBeInTheDocument();
  });

  it('keeps long product names discoverable and compact unit tiles selectable', () => {
    const onSelectUnit = vi.fn();
    const product = { id: 7, name: 'Galletas integrales avena y miel paquete familiar 300 g', salePrice: 7800, categoryName: 'Alimentos' };
    render(<ProductGrid weighedProducts={[]} unitProducts={[product]} onSelectUnit={onSelectUnit} />);
    const tile = screen.getByRole('button', { name: /Galletas integrales.*7.800/ });
    expect(tile).toHaveAttribute('title', product.name);
    fireEvent.click(tile);
    expect(onSelectUnit).toHaveBeenCalledWith(product);
  });
});

describe('Compact scale indicator', () => {
  it('keeps weight and disconnected status without a separate help paragraph or action buttons', () => {
    const { container } = render(<ScaleIndicator compact weight={0} isConnected={false} isStable={false} showActions={false} helperText="Configuración > Dispositivos" />);
    expect(screen.getByText('0.000 kg')).toBeInTheDocument();
    expect(screen.getByText('Desconectada')).toBeInTheDocument();
    expect(screen.queryByText('Configuración > Dispositivos')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Estado de la báscula')).toHaveAttribute('title', 'Configuración > Dispositivos');
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(container.querySelector('p')).toBeNull();
  });

  it('keeps connected stable weight and the configured unit', () => {
    render(<ScaleIndicator compact weight={1.25} unit="lb" isConnected isStable showActions={false} />);
    expect(screen.getByText('1.250 lb')).toBeInTheDocument();
    expect(screen.getByText('Peso estable')).toBeInTheDocument();
  });

  it('does not hide scale errors or unstable status to save space', () => {
    render(<ScaleIndicator compact weight={0.4} isConnected isStable={false} error="Lectura interrumpida" showActions={false} />);
    expect(screen.getByText('Peso inestable')).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Lectura interrumpida');
  });

  it('preserves optional connection controls', () => {
    const onConnect = vi.fn();
    render(<ScaleIndicator compact weight={0} isConnected={false} onConnect={onConnect} />);
    fireEvent.click(screen.getByRole('button', { name: 'Conectar báscula' }));
    expect(onConnect).toHaveBeenCalledOnce();
  });
});
