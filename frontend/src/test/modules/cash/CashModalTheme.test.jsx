import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import CloseCashRegisterModal from '../../../modules/sales/components/CloseCashRegisterModal';
import OpenCashRegisterModal from '../../../modules/sales/components/OpenCashRegisterModal';
import CashMovementModal from '../../../modules/sales/components/CashMovementModal';

const register = { id: 81, openingAmount: 5000, totalCashSales: 0, cashIn: 0, cashOut: 0, orderCount: 0 };

describe('Cash modal themed fields', () => {
  it('uses themed amount/notes fields and preserves the close payload', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();
    render(<CloseCashRegisterModal isOpen register={register} onConfirm={onConfirm} onClose={onClose} />);
    const amount = screen.getByRole('spinbutton', { name: 'Conteo real de efectivo' });
    const notes = screen.getByRole('textbox', { name: 'Notas de cierre (opcional)' });
    expect(amount).toHaveClass('input', 'pl-7');
    expect(notes).toHaveClass('input');
    expect(screen.getByRole('button', { name: 'Cerrar Caja' })).toBeDisabled();
    fireEvent.change(amount, { target: { value: '5000' } });
    fireEvent.change(notes, { target: { value: 'Arqueo confirmado' } });
    expect(screen.getByText('Cuadra perfectamente')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar Caja' }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith(81, { closingAmount: 5000, notes: 'Arqueo confirmado' }));
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  });

  it('uses the same themed fields when opening cash', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<OpenCashRegisterModal isOpen onConfirm={onConfirm} onClose={vi.fn()} />);
    const amount = screen.getByRole('spinbutton', { name: 'Monto base (efectivo)' });
    const notes = screen.getByRole('textbox', { name: 'Notas (opcional)' });
    expect(amount).toHaveClass('input', 'pl-7');
    expect(notes).toHaveClass('input');
    fireEvent.change(amount, { target: { value: '5000' } });
    fireEvent.change(notes, { target: { value: 'Turno tarde' } });
    fireEvent.click(screen.getByRole('button', { name: 'Abrir Caja' }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith({ openingAmount: 5000, notes: 'Turno tarde' }));
  });

  it.each(['in', 'out'])('keeps movement %s fields readable through the shared input style', async type => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<CashMovementModal isOpen type={type} registerId={81} onConfirm={onConfirm} onClose={vi.fn()} />);
    const amount = screen.getByRole('spinbutton');
    fireEvent.click(screen.getByRole('button', { name: 'Otro...' }));
    const reason = screen.getByPlaceholderText('Describe el motivo');
    const notes = screen.getByPlaceholderText('Detalle adicional');
    [amount, reason, notes].forEach(field => expect(field).toHaveClass('input'));
    fireEvent.change(amount, { target: { value: '500' } });
    fireEvent.change(reason, { target: { value: 'Movimiento autorizado' } });
    fireEvent.change(notes, { target: { value: 'Detalle' } });
    fireEvent.submit(amount.closest('form'));
    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith(81, { type, amount: 500, reason: 'Movimiento autorizado', notes: 'Detalle' }));
  });

  it('canceling still closes the modal without sending a mutation', () => {
    const onConfirm = vi.fn();
    const onClose = vi.fn();
    render(<CloseCashRegisterModal isOpen register={register} onConfirm={onConfirm} onClose={onClose} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
