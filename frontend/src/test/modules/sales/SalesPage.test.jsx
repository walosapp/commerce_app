import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SalesPage from '../../../modules/sales/SalesPage';

const { invoiceTable, enqueuePostSale, invalidateQueries, toastSuccess, toastError } = vi.hoisted(() => ({
  invoiceTable: vi.fn(),
  enqueuePostSale: vi.fn(),
  invalidateQueries: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
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
  useQuery: ({ queryKey }) => {
    if (queryKey[0] === 'sales-tables') return { data: { data: [table] }, isLoading: false };
    if (queryKey[0] === 'stock') return { data: { data: [] }, isLoading: false };
    return { data: { data: null }, isLoading: false };
  },
}));

vi.mock('../../../services/salesService', () => ({
  default: {
    getTables: vi.fn(),
    invoiceTable,
  },
}));

vi.mock('../../../services/cashRegisterService', () => ({
  cashRegisterService: { getActive: vi.fn() },
}));

vi.mock('../../../services/inventoryService', () => ({
  default: { getStock: vi.fn() },
}));

vi.mock('../../../stores/authStore', () => ({
  default: () => ({ branchId: 7 }),
}));

vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale }),
}));

vi.mock('react-hot-toast', () => ({
  default: { success: toastSuccess, error: toastError },
}));

vi.mock('../../../modules/sales/components/TableCard', () => ({
  default: ({ table: target, onInvoice }) => (
    <button onClick={() => onInvoice(target)}>Facturar mesa</button>
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

vi.mock('../../../modules/sales/components/AddTablePanel', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CreditsPanel', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/SalesSummaryTab', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashRegisterBar', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/OpenCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CloseCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashMovementModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashRegisterHistory', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/OrderHistoryTab', () => ({ default: () => null }));

const confirmSale = async () => {
  fireEvent.click(screen.getAllByRole('button', { name: 'Facturar mesa' })[0]);
  fireEvent.click(await screen.findByRole('button', { name: 'Confirmar venta' }));
};

describe('SalesPage postventa restaurante H3', () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
});
