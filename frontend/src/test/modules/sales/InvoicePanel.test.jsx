import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import InvoicePanel from '../../../modules/sales/components/InvoicePanel';

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => ({
    data: {
      data: {
        manualDiscountEnabled: true,
        maxDiscountPercent: 100,
        maxDiscountAmount: 1000000,
        discountRequiresOverride: false,
      },
    },
  }),
}));

vi.mock('../../../stores/authStore', () => ({
  default: () => ({ tenantId: 25 }),
}));

describe('InvoicePanel impresion previa H3', () => {
  const documentWrite = vi.fn();
  const documentClose = vi.fn();
  const browserPrint = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(window, 'open').mockReturnValue({
      document: { write: documentWrite, close: documentClose },
      print: browserPrint,
    });
  });

  it('conserva la impresion local solo como borrador claramente no valido', () => {
    const onConfirm = vi.fn();
    render(
      <InvoicePanel
        isOpen
        onClose={vi.fn()}
        onConfirm={onConfirm}
        table={{
          id: 12,
          tableNumber: 3,
          name: 'Mesa 3',
          total: 5000,
          items: [{ id: 1, productName: 'Cafe', quantity: 1, unitPrice: 5000 }],
        }}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: 'Imprimir borrador con navegador' }));

    expect(documentWrite).toHaveBeenCalledWith(expect.stringContaining('BORRADOR - NO VÁLIDO COMO RECIBO'));
    expect(documentWrite).toHaveBeenCalledWith(expect.stringContaining('<title>Borrador Mesa 3</title>'));
    expect(browserPrint).toHaveBeenCalledTimes(1);
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
