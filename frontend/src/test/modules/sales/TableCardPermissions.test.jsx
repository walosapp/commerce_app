import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import TableCard from '../../../modules/sales/components/TableCard';

const table = {
  id: 12,
  tableNumber: 3,
  createdAt: new Date().toISOString(),
  items: [{ id: 1, orderId: 44, productName: 'Cafe', quantity: 1, unitPrice: 5000 }],
};

describe('TableCard action capabilities', () => {
  it('does not render table cancellation without the capability callback', () => {
    render(<TableCard table={table} />);

    expect(screen.queryByTitle('Cancelar mesa')).not.toBeInTheDocument();
  });

  it('renders table cancellation for authorized operators', () => {
    const onCancel = vi.fn();
    render(<TableCard table={table} onCancel={onCancel} />);

    fireEvent.click(screen.getByTitle('Cancelar mesa'));
    expect(onCancel).toHaveBeenCalledWith(table);
  });

  it('prints the active restaurant order without depending on invoice capability', () => {
    const onPrintKitchen = vi.fn();
    render(<TableCard table={table} onPrintKitchen={onPrintKitchen} />);

    fireEvent.click(screen.getByTitle('Imprimir comanda'));
    expect(onPrintKitchen).toHaveBeenCalledWith(44);
  });
});
