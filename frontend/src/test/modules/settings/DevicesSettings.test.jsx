import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import DevicesSettings from '../../../modules/settings/components/DevicesSettings';
import useDeviceStore from '../../../stores/deviceStore';

vi.mock('../../../modules/settings/components/PrinterSettings', () => ({
  default: () => <div>Configuracion del Print Agent activa</div>,
}));

describe('DevicesSettings', () => {
  beforeEach(() => {
    useDeviceStore.setState({ selectedDeviceType: 'scale' });
  });

  it('integra la configuracion de impresora solamente en Dispositivos', () => {
    render(<DevicesSettings />);

    fireEvent.click(screen.getByRole('button', { name: /Impresora/ }));

    expect(screen.getByText('Configuracion del Print Agent activa')).toBeInTheDocument();
    expect(screen.getByText('Configuracion de Impresora')).toBeInTheDocument();
  });
});
