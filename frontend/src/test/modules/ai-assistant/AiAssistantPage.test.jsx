import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AiAssistantPage from '../../../modules/ai-assistant/AiAssistantPage';

const { featureState, queryConfigs, chatProps } = vi.hoisted(() => ({
  featureState: { canAccess: vi.fn() },
  queryConfigs: [],
  chatProps: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (config) => {
    queryConfigs.push(config);
    return { data: { count: 2, data: [{ id: 1, product_name: 'Café', quantity: 1, unit: 'unidad' }] } };
  },
}));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));
vi.mock('../../../stores/authStore', () => ({ default: () => ({ branchId: 7 }) }));
vi.mock('../../../modules/ai-assistant/components/AIChat', () => ({
  default: (props) => {
    chatProps.push(props);
    return <div>Chat IA activo</div>;
  },
}));
vi.mock('react-hot-toast', () => ({ default: { success: vi.fn() } }));

describe('AiAssistantPage feature isolation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
    chatProps.length = 0;
  });

  it('keeps AI chat but does not query or show stock when inventory is disabled', () => {
    featureState.canAccess.mockImplementation((code) => code === 'ai');

    render(<AiAssistantPage />);

    expect(screen.getByText('Chat IA activo')).toBeInTheDocument();
    expect(queryConfigs.find((query) => query.queryKey[0] === 'lowStock')?.enabled).toBe(false);
    expect(screen.queryByText(/Productos con Stock Bajo/i)).not.toBeInTheDocument();
    expect(chatProps.at(-1).enabledCapabilities).toEqual([]);
  });

  it('enables the stock supplement only when inventory is accessible', () => {
    featureState.canAccess.mockImplementation((code) => ['ai', 'inventory'].includes(code));

    render(<AiAssistantPage />);

    expect(queryConfigs.find((query) => query.queryKey[0] === 'lowStock')?.enabled).toBe(true);
    expect(screen.getByText(/Productos con Stock Bajo/)).toBeInTheDocument();
    expect(chatProps.at(-1).enabledCapabilities).toEqual(['inventory']);
  });

  it('passes only enabled AI capabilities to the chat', () => {
    featureState.canAccess.mockImplementation((code) => ['ai', 'purchases', 'delivery'].includes(code));

    render(<AiAssistantPage />);

    expect(chatProps.at(-1).enabledCapabilities).toEqual(['purchases', 'delivery']);
  });

  it('does not advertise disabled module tips', () => {
    featureState.canAccess.mockImplementation((code) => code === 'ai');
    render(<AiAssistantPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Ayuda' }));

    expect(screen.queryByText(/Consulta stock y alertas/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/información disponible de tus proveedores/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/conceptos financieros/i)).not.toBeInTheDocument();
  });
});
