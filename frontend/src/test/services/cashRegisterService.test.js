import { describe, expect, it, vi } from 'vitest';
import { cashRegisterService } from '../../services/cashRegisterService';
import api from '../../config/api';

vi.mock('../../config/api', () => ({ default: { get: vi.fn() } }));

describe('cash status HTTP contract', () => {
  it('consulta el endpoint mínimo sin sustituirlo por active financiero', async () => {
    api.get.mockResolvedValue({ data: { success: true, data: { branchId: 91, status: 'open' } } });
    expect(await cashRegisterService.getStatus()).toEqual({ success: true, data: { branchId: 91, status: 'open' } });
    expect(api.get).toHaveBeenCalledTimes(1);
    expect(api.get).toHaveBeenCalledWith('/sales/cash-register/status');
  });
});
