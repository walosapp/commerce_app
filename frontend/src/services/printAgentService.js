const PRINT_AGENT_BASE_URL = 'http://127.0.0.1:17831';
const DEFAULT_TIMEOUT_MS = 3500;

export class PrintAgentError extends Error {
  constructor(message, { status = null, code = null, cause = null } = {}) {
    super(message, { cause });
    this.name = 'PrintAgentError';
    this.status = status;
    this.code = code;
  }
}

const request = async (path, { method = 'GET', token, body, timeoutMs = DEFAULT_TIMEOUT_MS } = {}) => {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(`${PRINT_AGENT_BASE_URL}${path}`, {
      method,
      mode: 'cors',
      cache: 'no-store',
      credentials: 'omit',
      signal: controller.signal,
      headers: {
        Accept: 'application/json',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      ...(body ? { body: JSON.stringify(body) } : {}),
    });

    const contentType = response.headers.get('content-type') || '';
    const payload = contentType.includes('application/json') ? await response.json() : null;

    if (!response.ok) {
      throw new PrintAgentError(
        payload?.message || payload?.error || `El agente respondio HTTP ${response.status}`,
        { status: response.status, code: payload?.code || null }
      );
    }

    return payload;
  } catch (error) {
    if (error instanceof PrintAgentError) throw error;

    const timedOut = error?.name === 'AbortError';
    throw new PrintAgentError(
      timedOut ? 'El agente de impresion no respondio a tiempo.' : 'Walos Print Agent no esta disponible en este equipo.',
      { code: timedOut ? 'agent_timeout' : 'agent_unavailable', cause: error }
    );
  } finally {
    clearTimeout(timeout);
  }
};

const printerConfigPayload = (config) => ({
  companyId: Number(config.companyId),
  branchId: Number(config.branchId),
  workstationId: String(config.workstationId || '').trim(),
  printerName: String(config.printerName || '').trim(),
  drawerPin: Number(config.drawerPin),
  drawerOnTimeMs: Number(config.drawerOnTimeMs),
  drawerOffTimeMs: Number(config.drawerOffTimeMs),
});

export const printAgentService = {
  health: () => request('/v1/health'),

  pair: ({ pairingCode, companyId, branchId, workstationId }) =>
    request('/v1/pair', {
      method: 'POST',
      body: {
        pairingCode: String(pairingCode || '').trim(),
        companyId: Number(companyId),
        branchId: Number(branchId),
        workstationId: String(workstationId || '').trim(),
      },
    }),

  getPrinters: (token) => request('/v1/printers', { token }),

  savePrinterConfig: (token, config) =>
    request('/v1/config/printer', {
      method: 'PUT',
      token,
      body: printerConfigPayload(config),
    }),

  testPrint: (token, jobId) =>
    request('/v1/commands/test-print', {
      method: 'POST',
      token,
      body: { jobId: String(jobId) },
      timeoutMs: 10000,
    }),

  openDrawer: (token, jobId) =>
    request('/v1/commands/open-drawer', {
      method: 'POST',
      token,
      body: { jobId: String(jobId) },
      timeoutMs: 10000,
    }),
};

export default printAgentService;
