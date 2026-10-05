import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SalesPage from '../../../modules/sales/SalesPage';

const { cancelTable, invoiceTable, getSaleCatalog, enqueuePostSale, invalidateQueries, toastSuccess, toastError, featureState, queryConfigs, authState, cashState, catalogState } = vi.hoisted(() => ({
  cancelTable: vi.fn(),
  invoiceTable: vi.fn(),
  getSaleCatalog: vi.fn(),
  enqueuePostSale: vi.fn(),
  invalidateQueries: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
  featureState: { canAccess: vi.fn(), hasFeature: vi.fn(), isReady: true },
  queryConfigs: [],
  authState: { isAuthenticated: true, branchId: 7, tenantId: 3, user: { role: 'manager', isPlatformAdmin: false } },
  cashState: { register: null, isLoading: false },
  catalogState: { items: [] },
}));

const table = {
  id: 12,
  tableNumber: 3,
  name: 'Mesa 3',
  items: [{ id: 1, orderId: 44, productId: 5, productName: 'Cafe', quantity: 1, unitPrice: 5000 }],
  total: 5000,
};

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries }),
  useQuery: (config) => {
    queryConfigs.push(config);
    const { queryKey } = config;
    if (queryKey[0] === 'sales-tables') return { data: { data: [table] }, isLoading: false };
    if (queryKey[0] === 'sale-catalog') return { data: { data: catalogState.items }, isLoading: false };
    if (queryKey[0] === 'cash-register-status') {
      return {
        data: { data: { branchId: 7, status: cashState.register ? 'open' : 'closed' } },
        isLoading: cashState.isLoading,
        isError: cashState.isError,
        isSuccess: !cashState.isLoading && !cashState.isError,
        refetch: cashState.refetch,
      };
    }
    return { data: { data: null }, isLoading: false };
  },
}));

vi.mock('../../../services/salesService', () => ({
  default: {
    getTables: vi.fn(),
    cancelTable,
    invoiceTable,
  },
}));

vi.mock('../../../services/cashRegisterService', () => ({
  cashRegisterService: { getStatus: vi.fn() },
}));

vi.mock('../../../services/inventoryService', () => ({
  default: { getSaleCatalog },
}));

vi.mock('../../../stores/authStore', () => ({
  default: () => authState,
}));

vi.mock('../../../hooks/useCompanyFeatures', () => ({
  default: () => featureState,
}));

vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale }),
}));

vi.mock('react-hot-toast', () => ({
  default: { success: toastSuccess, error: toastError },
}));

vi.mock('../../../modules/sales/components/TableCard', () => ({
  default: ({ table: target, onCancel, onInvoice, onPrintKitchen }) => (
    <div>
      {onInvoice
        ? <button onClick={() => onInvoice(target)}>Facturar mesa</button>
        : <span>Mesa operativa sin cobro</span>}
      {onCancel && <button onClick={() => onCancel(target)}>Cancelar mesa</button>}
      {onPrintKitchen && <button onClick={() => onPrintKitchen(target.items[0].orderId)}>Imprimir comanda</button>}
    </div>
  ),
}));

vi.mock('../../../modules/sales/components/InvoicePanel', () => ({
  default: ({ isOpen, onClose, onConfirm, table: target }) => isOpen ? (
    <button onClick={async () => {
      await onConfirm(target.id, { payments: [{ method: 'cash', amount: 5000 }] });
      onClose();
    }}>
      Confirmar venta
    </button>
  ) : null,
}));

vi.mock('../../../modules/sales/components/AddTablePanel', () => ({
  default: ({ isOpen, products }) => isOpen
    ? <div>{products.map((product) => <span key={product.productId}>{product.productName}</span>)}</div>
    : null,
}));
vi.mock('../../../modules/sales/components/CreditsPanel', () => ({ default: () => <div>Panel de créditos</div> }));
vi.mock('../../../modules/sales/components/SalesSummaryTab', () => ({ default: ({ canRefund }) => <div>{canRefund ? 'Puede devolver' : 'Solo consulta'}</div> }));
vi.mock('../../../modules/sales/components/OpenCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CloseCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashMovementModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashRegisterHistory', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/OrderHistoryTab', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/KitchenTicket', () => ({
  default: ({ orderId, scope }) => <div>Comanda {scope} {orderId}</div>,
}));

const confirmSale = async () => {
  fireEvent.click(screen.getAllByRole('button', { name: 'Facturar mesa' })[0]);
  fireEvent.click(await screen.findByRole('button', { name: 'Confirmar venta' }));
};

describe('SalesPage postventa restaurante H3', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
    authState.user = { role: 'manager', isPlatformAdmin: false };
    cashState.register = null;
    cashState.isLoading = false;
    cashState.isError = false;
    cashState.refetch = vi.fn();
    catalogState.items = [];
    featureState.canAccess.mockReturnValue(true);
    featureState.hasFeature.mockReturnValue(true);
    getSaleCatalog.mockResolvedValue({ data: [] });
    cancelTable.mockResolvedValue({ success: true });
    invoiceTable.mockResolvedValue({
      data: {
        orderId: 987,
        orderNumber: 'ORD-987',
        isReplay: false,
      },
    });
    enqueuePostSale.mockResolvedValue({ status: 'completed' });
  });

  it('encola hardware con el orderId persistido despues de confirmar la venta', async () => {
    render(<SalesPage />);

    await confirmSale();

    await waitFor(() => expect(enqueuePostSale).toHaveBeenCalledWith({ orderId: 987 }));
    expect(invoiceTable).toHaveBeenCalledTimes(1);
    expect(toastSuccess).toHaveBeenCalledWith('Venta registrada');
    expect(screen.queryByRole('button', { name: 'Confirmar venta' })).not.toBeInTheDocument();
  });

  it('cierra la venta sin esperar a que terminen los efectos de hardware', async () => {
    enqueuePostSale.mockReturnValue(new Promise(() => {}));
    render(<SalesPage />);

    await confirmSale();

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Confirmar venta' })).not.toBeInTheDocument());
    expect(invoiceTable).toHaveBeenCalledTimes(1);
    expect(enqueuePostSale).toHaveBeenCalledTimes(1);
  });

  it('no convierte un fallo postventa en fallo de checkout ni repite la orden', async () => {
    enqueuePostSale.mockRejectedValue(new Error('Agente desconectado'));
    render(<SalesPage />);

    await confirmSale();

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Confirmar venta' })).not.toBeInTheDocument());
    expect(invoiceTable).toHaveBeenCalledTimes(1);
    expect(toastSuccess).toHaveBeenCalledWith('Venta registrada');
    expect(toastError).not.toHaveBeenCalled();
  });

  it('aisla tambien un fallo sincrono del almacenamiento local postventa', async () => {
    enqueuePostSale.mockImplementationOnce(() => { throw new Error('localStorage bloqueado'); });
    render(<SalesPage />);

    await confirmSale();

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Confirmar venta' })).not.toBeInTheDocument());
    expect(invoiceTable).toHaveBeenCalledTimes(1);
    expect(toastSuccess).toHaveBeenCalledWith('Venta registrada');
    expect(toastError).not.toHaveBeenCalled();
  });

  it('usa el mismo orderId persistido cuando backend informa replay de la venta', async () => {
    invoiceTable.mockResolvedValue({ data: { orderId: 987, orderNumber: 'ORD-987', isReplay: true } });
    render(<SalesPage />);

    await confirmSale();

    await waitFor(() => expect(enqueuePostSale).toHaveBeenCalledWith({ orderId: 987 }));
    expect(invoiceTable).toHaveBeenCalledTimes(1);
  });

  it('no inicia caja cuando el feature cash esta apagado', () => {
    featureState.canAccess.mockImplementation((code) => code === 'restaurant');
    featureState.hasFeature.mockReturnValue(false);

    render(<SalesPage />);

    expect(queryConfigs.find((config) => config.queryKey[0] === 'cash-register-status')?.enabled).toBe(false);
    expect(queryConfigs.find((config) => config.queryKey[0] === 'sales-tables')?.enabled).toBe(true);
    expect(screen.queryByRole('button', { name: /Caja/i })).not.toBeInTheDocument();
  });

  it('loads the sale-safe catalog instead of the inventory module stock endpoint', async () => {
    featureState.canAccess.mockImplementation((code) => code === 'restaurant');

    render(<SalesPage />);

    const catalogQuery = queryConfigs.find((config) => config.queryKey[0] === 'sale-catalog');
    expect(catalogQuery.enabled).toBe(true);
    expect(catalogQuery.queryKey).toEqual(['sale-catalog', 3, 7]);
    await catalogQuery.queryFn();
    expect(getSaleCatalog).toHaveBeenCalledWith(7);
  });

  it.each(['waiter', 'cashier'])('loads the restaurant sales catalog for %s without inventory administration', async (role) => {
    authState.user = { role, isPlatformAdmin: false };
    featureState.canAccess.mockImplementation((code) => code === 'restaurant');
    catalogState.items = [{
      productId: 501,
      productName: `Café ${role}`,
      isConfiguredForSale: true,
      trackStock: false,
      availableQuantity: 0,
    }];

    render(<SalesPage />);

    const catalogQuery = queryConfigs.find((config) => config.queryKey[0] === 'sale-catalog');
    expect(catalogQuery.enabled).toBe(true);
    expect(catalogQuery.queryKey).toEqual(['sale-catalog', 3, 7]);
    fireEvent.click(screen.getByRole('button', { name: 'Agregar Mesa' }));
    expect(screen.getByText(`Café ${role}`)).toBeInTheDocument();
    await catalogQuery.queryFn();
    expect(getSaleCatalog).toHaveBeenCalledWith(7);
  });


  it('keeps waiter on table operations without invoice, credits, refunds or sales history', () => {
    authState.user = { role: 'waiter', isPlatformAdmin: false };
    featureState.canAccess.mockImplementation((code) => code === 'restaurant');

    render(<SalesPage />);

    expect(screen.queryByRole('button', { name: /Créditos/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Facturar mesa' })).not.toBeInTheDocument();
    expect(screen.getAllByText('Mesa operativa sin cobro').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /Ventas/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Historial/i })).not.toBeInTheDocument();
    expect(invoiceTable).not.toHaveBeenCalled();
    expect(cancelTable).not.toHaveBeenCalled();

    fireEvent.click(screen.getAllByRole('button', { name: 'Imprimir comanda' })[0]);
    expect(screen.getByText('Comanda activeRestaurant 44')).toBeInTheDocument();
  });

  it('allows cashier cash operations when the company modules are enabled', () => {
    authState.user = { role: 'cashier', isPlatformAdmin: false };
    featureState.canAccess.mockImplementation((code) => ['restaurant', 'cash'].includes(code));

    render(<SalesPage />);

    expect(screen.getByRole('button', { name: /Créditos/i })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Facturar mesa' }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: 'Cancelar mesa' }).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Ventas/i }));
    expect(screen.getByText('Puede devolver')).toBeInTheDocument();
  });

  it.each(['waiter', 'cashier'])('solo consulta estado de caja para %s, sin gestión ni resumen financiero', async (role) => {
    authState.user = { role, isPlatformAdmin: false };
    cashState.register = { id: 81, totalSales: 999999 };
    featureState.canAccess.mockImplementation(code => code === 'restaurant');
    render(<SalesPage />);
    expect(screen.getByText('Caja: ABIERTA')).toBeInTheDocument();
    const status = queryConfigs.find(config => config.queryKey[0] === 'cash-register-status');
    expect(status.enabled).toBe(true);
    expect(status.queryKey).toEqual(['cash-register-status', 3, 7]);
    expect(queryConfigs.some(config => config.queryKey[0] === 'cash-register-active')).toBe(false);
    for (const name of ['Abrir Caja', 'Cerrar Caja', 'Entrada', 'Salida', 'Caja']) {
      expect(screen.queryByRole('button', { name, exact: true })).not.toBeInTheDocument();
    }
    expect(screen.queryByText('Turno actual')).not.toBeInTheDocument();
    expect(screen.queryByText('Ventas totales')).not.toBeInTheDocument();
  });

  it('retry del estado no permite apertura desde Restaurante', () => {
    cashState.isError = true;
    render(<SalesPage />);
    expect(screen.getByText('Caja: NO SE PUDO VERIFICAR')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar caja' }));
    expect(cashState.refetch).toHaveBeenCalledOnce();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
  });

  it('desmonta resumen de ventas al cambiar de cajero a mesero', () => {
    const view = render(<SalesPage />);
    fireEvent.click(screen.getByRole('button', { name: 'Ventas' }));
    expect(screen.getByText('Puede devolver')).toBeInTheDocument();
    authState.user = { role: 'waiter', isPlatformAdmin: false };
    view.rerender(<SalesPage />);
    expect(screen.queryByText('Puede devolver')).not.toBeInTheDocument();
    expect(screen.queryByText('Solo consulta')).not.toBeInTheDocument();
    expect(screen.getAllByText('Mesa operativa sin cobro').length).toBeGreaterThan(0);
  });
});
