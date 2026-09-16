import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import DeliveryOrderCard from '../../../modules/delivery/components/DeliveryOrderCard';
import DeliveryOrderDetailsPanel from '../../../modules/delivery/components/DeliveryOrderDetailsPanel';

const { detailOrder } = vi.hoisted(() => ({
  detailOrder: { current: null },
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => ({ data: { data: detailOrder.current }, isLoading: false }),
}));

vi.mock('../../../services/deliveryService', () => ({
  default: { getOrder: vi.fn() },
}));

const order = (status) => ({
  id: 1,
  orderNumber: 'DEL-1',
  source: 'manual',
  status,
  createdAt: new Date().toISOString(),
  total: 1000,
});

describe('delivery action role policy', () => {
  it('keeps operational progression but hides reject from waiter cards', () => {
    const onAction = vi.fn();
    render(<DeliveryOrderCard order={order('new')} onClick={vi.fn()} onAction={onAction} canManage={false} />);

    expect(screen.getByRole('button', { name: /Aceptar/i })).toBeInTheDocument();
    expect(screen.queryByTitle('Rechazar')).not.toBeInTheDocument();
  });

  it('shows reject to manager cards', () => {
    render(<DeliveryOrderCard order={order('new')} onClick={vi.fn()} onAction={vi.fn()} canManage />);
    expect(screen.getByTitle('Rechazar')).toBeInTheDocument();
  });

  it.each([
    ['accepted', 'Rechazar'],
    ['preparing', 'Cancelar'],
    ['ready_for_dispatch', 'Cancelar'],
    ['out_for_delivery', 'Devolver'],
  ])('hides %s management action from non-managers', (status, label) => {
    detailOrder.current = order(status);
    render(<DeliveryOrderDetailsPanel orderId={1} onClose={vi.fn()} onAction={vi.fn()} canManage={false} />);
    expect(screen.queryByRole('button', { name: label })).not.toBeInTheDocument();
  });

  it('shows management actions to a manager', () => {
    const onAction = vi.fn();
    detailOrder.current = order('ready_for_dispatch');
    render(<DeliveryOrderDetailsPanel orderId={1} onClose={vi.fn()} onAction={onAction} canManage />);

    fireEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onAction).toHaveBeenCalledWith(detailOrder.current, 'cancel');
  });
});
