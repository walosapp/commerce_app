import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import RefundModal from '../../../modules/sales/components/RefundModal';

const order = {
  id: 17,
  orderNumber: 'ORD-17',
  finalTotalPaid: 40,
  discountAmount: 20,
  hasCredit: true,
  items: [
    { id: 101, productName: 'Producto', quantity: 2, unitPrice: 30 },
  ],
};

describe('RefundModal', () => {
  it('does not calculate a client-side refund amount', () => {
    render(<RefundModal isOpen onClose={vi.fn()} onConfirm={vi.fn()} order={order} />);

    expect(screen.queryByText('Monto a devolver')).not.toBeInTheDocument();
    expect(screen.getByText(/valor neto sera calculado por el backend/i)).toBeInTheDocument();
    expect(screen.getByText(/descuento registrado/i)).toHaveTextContent('$20');
    expect(screen.getByText(/primero al credito pendiente/i)).toBeInTheDocument();
    expect(screen.getByText(/dinero realmente devuelto se confirmaran/i)).toBeInTheDocument();
  });

  it('submits only the selected item intent for a partial refund', async () => {
    const onConfirm = vi.fn().mockResolvedValue({});
    render(<RefundModal isOpen onClose={vi.fn()} onConfirm={onConfirm} order={order} />);

    fireEvent.click(screen.getByRole('button', { name: /parcial/i }));
    fireEvent.click(screen.getByRole('checkbox'));
    fireEvent.change(screen.getByPlaceholderText(/describe el motivo/i), {
      target: { value: 'Producto entregado por error' },
    });
    fireEvent.click(screen.getByRole('button', { name: /confirmar devolucion/i }));

    await waitFor(() => expect(onConfirm).toHaveBeenCalledTimes(1));
    expect(onConfirm).toHaveBeenCalledWith(expect.objectContaining({
      orderId: 17,
      refundType: 'partial',
      reason: 'Producto entregado por error',
      items: [{ orderItemId: 101, quantity: 2 }],
    }));
  });
});
