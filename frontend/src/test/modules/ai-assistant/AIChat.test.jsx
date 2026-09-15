import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AIChat, { buildAiWelcomeMessage } from '../../../modules/ai-assistant/components/AIChat';
import aiService from '../../../services/aiService';
import toast from 'react-hot-toast';

vi.mock('../../../services/aiService', () => ({
  default: { chat: vi.fn() },
}));
vi.mock('react-hot-toast', () => ({ default: { error: vi.fn() } }));

describe('AIChat feature capabilities', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Element.prototype.scrollIntoView = vi.fn();
  });

  it('advertises only enabled capabilities and always keeps general chat', () => {
    const welcome = buildAiWelcomeMessage(['delivery']);

    expect(welcome).toContain('Domicilios');
    expect(welcome).toContain('General');
    expect(welcome).not.toContain('Inventario');
    expect(welcome).not.toContain('Compras');
    expect(welcome).not.toContain('Proveedores');
  });

  it('shows a stable message when a capability is disabled', async () => {
    aiService.chat.mockRejectedValue({
      response: { data: { code: 'feature_not_enabled', message: 'backend detail' } },
    });
    render(<AIChat enabledCapabilities={['inventory']} />);

    const input = screen.getByPlaceholderText('Escribe tu mensaje...');
    fireEvent.change(input, {
      target: { value: 'consulta stock' },
    });
    fireEvent.keyDown(input, { key: 'Enter' });

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(
      'Esa capacidad del asistente no está habilitada para este comercio.',
    ));
  });

  it('does not report a read-only response as a confirmed action', async () => {
    const onActionConfirmed = vi.fn();
    aiService.chat.mockResolvedValue({
      data: {
        sessionId: 12,
        agentType: 'general',
        responseType: 'text',
        message: 'Respuesta informativa',
      },
    });
    render(<AIChat enabledCapabilities={[]} onActionConfirmed={onActionConfirmed} />);

    const input = screen.getByPlaceholderText('Escribe tu mensaje...');
    fireEvent.change(input, { target: { value: 'consulta general' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    await screen.findByText('Respuesta informativa');
    expect(onActionConfirmed).not.toHaveBeenCalled();
  });
});
