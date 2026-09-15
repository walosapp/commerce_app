import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import PosDeliPage from '../../../modules/pos-deli/PosDeliPage';

const mocks = vi.hoisted(() => ({
  createSale: vi.fn(),
  enqueuePostSale: vi.fn(),
  refetch: vi.fn(),
  clearTicket: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
  storeState: null,
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: ({ queryKey }) => ({
    data: { data: [] },
    ...(queryKey[0] === 'pos-deli-products' ? { refetch: mocks.refetch } : {}),
  }),
}));

vi.mock('react-hot-toast', () => ({
  default: {
    success: mocks.toastSuccess,
    error: mocks.toastError,
  },
}));

vi.mock('../../../services/posDeliService', () => ({
  default: {
    getProducts: vi.fn(),
    getFavorites: vi.fn(),
    createSale: mocks.createSale,
  },
}));

vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale: mocks.enqueuePostSale }),
}));

vi.mock('../../../modules/pos-deli/stores/posDeliStore', () => ({
  default: () => mocks.storeState,
}));

vi.mock('../../../modules/pos-deli/hooks/useScale', () => ({
  default: () => ({
    weight: 0,
    isStable: false,
    isConnected: false,
    error: null,
    config: null,
  }),
}));

vi.mock('../../../modules/pos-deli/hooks/useBarcodeScanner', () => ({
  default: vi.fn(),
}));

vi.mock('../../../modules/pos-deli/components/ProductGrid', () => ({
  default: () => null,
}));

vi.mock('../../../modules/pos-deli/components/ProductSearchBar', () => ({
  default: () => null,
}));

vi.mock('../../../modules/pos-deli/components/ScaleIndicator', () => ({
  default: () => null,
}));

vi.mock('../../../modules/pos-deli/components/WeightInputModal', () => ({
  default: () => null,
}));

vi.mock('../../../modules/pos-deli/components/TicketPanel', () => ({
  default: ({ onCheckout }) => <button onClick={onCheckout}>Cobrar</button>,
}));

vi.mock('../../../modules/pos-deli/components/PaymentModal', () => ({
  default: ({ isOpen, onConfirm, loading }) => isOpen ? (
    <div>
      <button disabled={loading} onClick={() => onConfirm({ method: 'cash', cashReceived: 120, reference: '' })}>
        Confirmar efectivo
      </button>
      <button disabled={loading} onClick={() => onConfirm({ method: 'card', cashReceived: 0, reference: 'CARD-1' })}>
        Confirmar tarjeta
      </button>
    </div>
  ) : null,
}));

const renderCheckout = () => {
  render(<PosDeliPage />);
  fireEvent.click(screen.getByRole('button', { name: 'Cobrar' }));
};

describe('PosDeliPage H3 postventa', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.refetch.mockResolvedValue({ data: { data: [] } });
    mocks.enqueuePostSale.mockResolvedValue({ print: { status: 'completed' } });
    mocks.createSale.mockResolvedValue({
      data: { saleId: 932, ticketNumber: 'POS-932', total: 100, change: 20 },
    });
    mocks.storeState = {
      items: [{
        id: 'line-1',
        productId: 42,
        name: 'Producto',
        quantity: 1,
        unitPrice: 100,
        subtotal: 100,
        isWeighed: false,
      }],
      selectedItemId: null,
      getTotal: () => 100,
      addWeighedItem: vi.fn(),
      addUnitItem: vi.fn(),
      removeItem: vi.fn(),
      updateQuantity: vi.fn(),
      setSelectedItemId: vi.fn(),
      clearTicket: mocks.clearTicket,
    };
  });

  it('encola hardware solo con el orderId persistido', async () => {
    const openSpy = vi.spyOn(window, 'open').mockImplementation(() => null);
    renderCheckout();

    fireEvent.click(screen.getByRole('button', { name: 'Confirmar efectivo' }));

    await waitFor(() => expect(mocks.enqueuePostSale).toHaveBeenCalledWith({ orderId: 932 }));
    expect(mocks.createSale).toHaveBeenCalledWith({
      items: [{ productId: 42, quantity: 1, unitPrice: 100, isWeighed: false }],
      payments: [{ method: 'cash', amount: 100, reference: null }],
      cashReceived: 120,
    });
    expect(mocks.clearTicket).toHaveBeenCalledOnce();
    expect(mocks.toastSuccess).toHaveBeenCalledWith('Venta POS-932 registrada');
    expect(openSpy).not.toHaveBeenCalled();
    openSpy.mockRestore();
  });

  it('delega al recibo persistido la decision de cajon para un pago no efectivo', async () => {
    renderCheckout();

    fireEvent.click(screen.getByRole('button', { name: 'Confirmar tarjeta' }));

    await waitFor(() => expect(mocks.enqueuePostSale).toHaveBeenCalledWith({ orderId: 932 }));
    expect(mocks.createSale).toHaveBeenCalledWith(expect.objectContaining({
      payments: [{ method: 'card', amount: 100, reference: 'CARD-1' }],
      cashReceived: null,
    }));
    expect(mocks.enqueuePostSale).toHaveBeenCalledTimes(1);
  });

  it('un fallo postventa no revierte la UX ni repite checkout', async () => {
    mocks.enqueuePostSale.mockRejectedValueOnce(new Error('Agente no disponible'));
    renderCheckout();

    fireEvent.click(screen.getByRole('button', { name: 'Confirmar efectivo' }));

    await waitFor(() => expect(mocks.enqueuePostSale).toHaveBeenCalledOnce());
    await waitFor(() => expect(mocks.clearTicket).toHaveBeenCalledOnce());
    expect(mocks.createSale).toHaveBeenCalledOnce();
    expect(mocks.toastSuccess).toHaveBeenCalledWith('Venta POS-932 registrada');
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

  it('aisla un throw sincrono postventa y mantiene la venta confirmada', async () => {
    mocks.enqueuePostSale.mockImplementationOnce(() => { throw new Error('storage bloqueado'); });
    renderCheckout();

    fireEvent.click(screen.getByRole('button', { name: 'Confirmar efectivo' }));

    await waitFor(() => expect(mocks.clearTicket).toHaveBeenCalledOnce());
    expect(mocks.createSale).toHaveBeenCalledOnce();
    expect(mocks.toastSuccess).toHaveBeenCalledWith('Venta POS-932 registrada');
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

});
