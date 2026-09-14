import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ReceiptPreview from '../../../modules/sales/components/ReceiptPreview';

const { agent, persistedReceipt, getReceipt, openPrintWindow } = vi.hoisted(() => ({
  persistedReceipt: {
    companyId: 25,
    branchId: 7,
    companyName: 'Tienda Persistida',
    companyLegalName: null,
    companyPhone: null,
    companyTaxId: '900123456-1',
    companyAddress: 'Calle 1',
    currency: 'COP',
    timezone: 'America/Bogota',
    orderId: 123,
    orderNumber: 'ORD-123',
    status: 'completed',
    refundStatus: null,
    tableName: 'Mostrador',
    tableNumber: 1,
    createdAt: '2026-09-14T17:30:00Z',
    cashierName: 'Maria',
    items: [{ productName: 'Cafe', quantity: 1, unitPrice: 5000, subtotal: 5000 }],
    subtotal: 5000,
    discountType: null,
    discountValue: 0,
    discountAmount: 0,
    finalTotalPaid: 5000,
    tipAmount: 0,
    tipIncluded: false,
    splitCount: 1,
    payments: [{ method: 'cash', amount: 5000, reference: null }],
    hasCredit: false,
    creditStatus: null,
    creditOriginalTotal: null,
    creditAmountPaid: null,
    creditAmount: null,
    creditCustomerName: null,
  },
  getReceipt: vi.fn(),
  openPrintWindow: vi.fn(),
  agent: {
    status: 'connected',
    activeCommand: null,
    initializeContext: vi.fn(),
    checkHealth: vi.fn(),
    printReceipt: vi.fn(),
    retryReceipt: vi.fn(),
    acknowledgeReceiptAttempt: vi.fn(),
    pendingReceiptCommand: null,
    openDrawer: vi.fn(),
  },
}));

vi.mock('../../../services/printService', () => ({
  default: { getReceipt },
}));

vi.mock('../../../stores/authStore', () => ({
  default: (selector) => selector({ tenantId: 25, branchId: 7 }),
}));

vi.mock('../../../stores/printAgentStore', () => ({
  default: () => agent,
}));

vi.mock('../../../modules/sales/components/printStyles', () => ({
  thermalReceiptStyles: 'receipt-css',
  openPrintWindow,
}));

const renderPreview = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ReceiptPreview orderId={123} onClose={vi.fn()} />
    </QueryClientProvider>
  );
};

describe('ReceiptPreview impresion H2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    agent.status = 'connected';
    agent.activeCommand = null;
    agent.pendingReceiptCommand = null;
    getReceipt.mockResolvedValue({ data: persistedReceipt });
    agent.checkHealth.mockResolvedValue({ status: 'ok', paired: true });
    agent.printReceipt.mockResolvedValue({ status: 'completed', executed: true });
    agent.retryReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    globalThis.confirm = vi.fn(() => true);
  });

  it('usa el ReceiptData obtenido por GET del recibo persistido al imprimir con el agente', async () => {
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    await waitFor(() => expect(agent.printReceipt).toHaveBeenCalledWith(persistedReceipt));
    expect(getReceipt).toHaveBeenCalledWith(123);
    expect(screen.getByText('Recibo impreso correctamente con Walos.')).toBeInTheDocument();
    expect(openPrintWindow).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('separa total de venta, pagado y propina sin inventar un mensaje final', async () => {
    getReceipt.mockResolvedValue({ data: {
      ...persistedReceipt,
      subtotal: 10000,
      discountAmount: 1000,
      finalTotalPaid: 7000,
      tipAmount: 700,
      tipIncluded: true,
      hasCredit: true,
      creditStatus: 'pending',
      creditOriginalTotal: 9000,
      creditAmountPaid: 7000,
      creditAmount: 2000,
      creditCustomerName: 'Cliente persistido',
      payments: [{ method: 'cash', amount: 7700, reference: null }],
    } });

    renderPreview();

    expect(await screen.findByText('Total venta')).toBeInTheDocument();
    expect(screen.getByText('COP 9.000')).toBeInTheDocument();
    expect(screen.getByText('Pagado venta')).toBeInTheDocument();
    expect(screen.getByText('COP 7.000')).toBeInTheDocument();
    expect(screen.getByText('Propina incluida')).toBeInTheDocument();
    expect(screen.getByText('Crédito vigente')).toBeInTheDocument();
    expect(screen.getByText('Saldo crédito')).toBeInTheDocument();
    expect(screen.queryByText(/gracias por su visita/i)).not.toBeInTheDocument();
  });

  it('ofrece fallback explicito cuando el agente no esta disponible', async () => {
    agent.status = 'disconnected';
    agent.checkHealth.mockResolvedValue(null);
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    expect(await screen.findByText(/no esta disponible/i)).toBeInTheDocument();
    expect(agent.printReceipt).not.toHaveBeenCalled();
    expect(openPrintWindow).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con dialogo' }));
    expect(openPrintWindow).toHaveBeenCalledOnce();
  });

  it('no envia el recibo cuando el agente no esta vinculado', async () => {
    agent.checkHealth.mockResolvedValue({ status: 'ok', paired: false });
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    expect(await screen.findByText(/requiere vinculacion/i)).toBeInTheDocument();
    expect(agent.printReceipt).not.toHaveBeenCalled();
    expect(openPrintWindow).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('un error del agente no altera el recibo ni dispara checkout, fallback o cajon', async () => {
    const before = JSON.stringify(persistedReceipt);
    agent.printReceipt.mockRejectedValue(Object.assign(new Error('Impresora desconectada'), { status: 503 }));
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    expect(await screen.findByText('Impresora desconectada')).toBeInTheDocument();
    expect(JSON.stringify(persistedReceipt)).toBe(before);
    expect(getReceipt).toHaveBeenCalledTimes(1);
    expect(openPrintWindow).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('muestra replay sin volver a usar impresion de navegador ni abrir cajon', async () => {
    agent.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    expect(await screen.findByText(/no se imprimio nuevamente/i)).toBeInTheDocument();
    expect(openPrintWindow).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('no confunde un job fallido con un replay exitoso', async () => {
    agent.printReceipt.mockResolvedValue({ status: 'failed', executed: false });
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir con Walos' }));

    expect(await screen.findByText(/estado failed/i)).toBeInTheDocument();
    expect(screen.queryByText(/ya habia sido procesado/i)).not.toBeInTheDocument();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('inhabilita impresion directa para una orden cancelada o devuelta', async () => {
    getReceipt.mockResolvedValue({ data: { ...persistedReceipt, refundStatus: 'full_refund' } });
    renderPreview();

    expect(await screen.findByText(/solo esta disponible para ordenes completadas sin devoluciones/i))
      .toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Imprimir con Walos' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Imprimir con dialogo' })).toBeDisabled();
    expect(agent.printReceipt).not.toHaveBeenCalled();
    expect(openPrintWindow).not.toHaveBeenCalled();
  });

  it('muestra un credito cancelado como no vigente sin ocultar su contabilidad', async () => {
    getReceipt.mockResolvedValue({ data: {
      ...persistedReceipt,
      subtotal: 20000,
      finalTotalPaid: 5000,
      payments: [{ method: 'cash', amount: 5000, reference: null }],
      hasCredit: true,
      creditStatus: 'cancelled',
      creditOriginalTotal: 20000,
      creditAmountPaid: 5000,
      creditAmount: 15000,
      creditCustomerName: 'Cliente cancelado',
    } });
    renderPreview();

    expect(await screen.findByText('Crédito cancelado')).toBeInTheDocument();
    expect(screen.getByText('Saldo no vigente')).toBeInTheDocument();
    expect(screen.getByText('NO VIGENTE / NO EXIGIBLE')).toBeInTheDocument();
    expect(screen.getAllByText('COP 15.000')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Imprimir con Walos' })).toBeEnabled();
  });

  it('renderiza la fecha persistida en la zona horaria y moneda del recibo', async () => {
    renderPreview();

    expect(await screen.findByText(/12:30/)).toBeInTheDocument();
    expect(screen.getAllByText('COP 5.000').length).toBeGreaterThan(0);
  });

  it('ofrece reintento explicito del mismo trabajo incierto', async () => {
    agent.pendingReceiptCommand = { orderId: 123, jobId: 'same-job', fingerprint: 'a'.repeat(64) };
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Reintentar mismo trabajo' }));

    await waitFor(() => expect(agent.retryReceipt).toHaveBeenCalledWith(persistedReceipt));
    expect(getReceipt).toHaveBeenCalledTimes(2);
    expect(agent.printReceipt).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('recarga el recibo persistido y no reintenta silenciosamente un documento devuelto', async () => {
    const refunded = { ...persistedReceipt, refundStatus: 'full_refund' };
    getReceipt
      .mockResolvedValueOnce({ data: persistedReceipt })
      .mockResolvedValueOnce({ data: refunded });
    agent.pendingReceiptCommand = { orderId: 123, jobId: 'same-job', fingerprint: 'a'.repeat(64) };
    agent.retryReceipt.mockRejectedValue(Object.assign(
      new Error('El recibo persistido cambió desde el intento anterior.'),
      { code: 'receipt_changed' }
    ));
    renderPreview();
    await screen.findByText('Tienda Persistida');

    fireEvent.click(screen.getByRole('button', { name: 'Reintentar mismo trabajo' }));

    await waitFor(() => expect(agent.retryReceipt).toHaveBeenCalledWith(refunded));
    expect(await screen.findByText(/recibo persistido cambió/i)).toBeInTheDocument();
    expect(agent.printReceipt).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });

  it('solo descarta un intento incierto tras advertencia y confirmacion explicitas', async () => {
    agent.pendingReceiptCommand = { orderId: 123, jobId: 'same-job', fingerprint: 'a'.repeat(64) };
    renderPreview();

    expect(await screen.findByText(/resultado físico es incierto/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Imprimir con dialogo' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Marcar intento como revisado' }));

    expect(globalThis.confirm).toHaveBeenCalledOnce();
    expect(agent.acknowledgeReceiptAttempt).toHaveBeenCalledOnce();
    expect(agent.retryReceipt).not.toHaveBeenCalled();
    expect(agent.openDrawer).not.toHaveBeenCalled();
  });
});
