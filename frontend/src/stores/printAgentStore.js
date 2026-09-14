import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import printAgentService from '../services/printAgentService';

const createWorkstationId = () => {
  if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID();
  return `walos-${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const createJobId = () => {
  if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID();
  return `job-${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const INITIAL_CONFIG = {
  companyId: '',
  branchId: '',
  workstationId: '',
  printerName: '',
  drawerPin: 0,
  drawerOnTimeMs: 120,
  drawerOffTimeMs: 240,
};

const errorMessage = (error) => error?.message || 'No se pudo comunicar con Walos Print Agent.';

export const createPrintAgentState = (service = printAgentService) => (set, get) => ({
  status: 'unknown',
  isChecking: false,
  isPairing: false,
  isLoadingPrinters: false,
  isSaving: false,
  activeCommand: null,
  version: null,
  agentId: null,
  agentPaired: false,
  token: null,
  pairedContext: null,
  printers: [],
  config: INITIAL_CONFIG,
  error: null,
  lastCommand: null,
  configurationSaved: false,

  initializeContext: ({ companyId, branchId }) =>
    set((state) => {
      const nextCompanyId = companyId ?? '';
      const nextBranchId = branchId ?? '';
      const contextChanged = state.pairedContext && (
        Number(state.pairedContext.companyId) !== Number(nextCompanyId) ||
        Number(state.pairedContext.branchId) !== Number(nextBranchId)
      );

      return {
        ...(contextChanged ? { token: null, agentId: null, agentPaired: false, printers: [], configurationSaved: false } : {}),
        config: {
          ...state.config,
          companyId: nextCompanyId,
          branchId: nextBranchId,
          workstationId: state.config.workstationId || createWorkstationId(),
          ...(contextChanged ? { printerName: '' } : {}),
        },
      };
    }),

  updateConfig: (partial) => set((state) => ({
    config: { ...state.config, ...partial },
    configurationSaved: false,
  })),
  clearError: () => set({ error: null }),

  checkHealth: async () => {
    set({ isChecking: true, error: null });
    try {
      const health = await service.health();
      set({
        status: 'connected',
        isChecking: false,
        version: health?.version || null,
        agentPaired: Boolean(health?.paired),
      });
      if (get().token) {
        try {
          await get().loadPrinters();
        } catch {
          // Health sigue valido aunque un token anterior ya no sea aceptado.
        }
      }
      return health;
    } catch (error) {
      set({
        status: 'disconnected',
        isChecking: false,
        version: null,
        agentPaired: false,
        printers: [],
        error: errorMessage(error),
      });
      return null;
    }
  },

  pair: async (pairingCode) => {
    const { config } = get();
    set({ isPairing: true, error: null });
    try {
      const result = await service.pair({ ...config, pairingCode });
      set({
        token: result.token,
        agentId: result.agentId || null,
        pairedContext: {
          companyId: config.companyId,
          branchId: config.branchId,
          workstationId: config.workstationId,
        },
        agentPaired: true,
        status: 'connected',
        isPairing: false,
        configurationSaved: false,
      });
      return result;
    } catch (error) {
      set({ isPairing: false, error: errorMessage(error) });
      throw error;
    }
  },

  loadPrinters: async () => {
    const { token } = get();
    if (!token) {
      set({ error: 'Vincula este navegador con el agente antes de listar impresoras.' });
      return null;
    }

    set({ isLoadingPrinters: true, error: null });
    try {
      const result = await service.getPrinters(token);
      set((state) => {
        const printerName = state.config.printerName || result?.selectedPrinter || '';
        return {
          printers: Array.isArray(result?.printers) ? result.printers : [],
          isLoadingPrinters: false,
          config: { ...state.config, printerName },
          configurationSaved: state.configurationSaved && printerName === state.config.printerName,
        };
      });
      return result;
    } catch (error) {
      set({
        isLoadingPrinters: false,
        error: errorMessage(error),
        ...(error?.status === 401 ? {
          token: null,
          agentId: null,
          agentPaired: false,
          pairedContext: null,
          printers: [],
          configurationSaved: false,
        } : {}),
      });
      throw error;
    }
  },

  saveConfig: async () => {
    const { token, config } = get();
    if (!token) throw new Error('El agente no esta vinculado.');

    set({ isSaving: true, error: null });
    try {
      const saved = await service.savePrinterConfig(token, config);
      set((state) => ({
        config: { ...state.config, ...saved },
        isSaving: false,
        configurationSaved: true,
      }));
      return saved;
    } catch (error) {
      set({
        isSaving: false,
        error: errorMessage(error),
        ...(error?.status === 401 ? {
          token: null,
          agentId: null,
          agentPaired: false,
          pairedContext: null,
          printers: [],
          configurationSaved: false,
        } : {}),
      });
      throw error;
    }
  },

  executeCommand: async (command) => {
    const { token, configurationSaved } = get();
    if (!token) throw new Error('El agente no esta vinculado.');
    if (!configurationSaved) throw new Error('Guarda la configuracion antes de ejecutar comandos fisicos.');

    const jobId = createJobId();
    const execute = command === 'test-print' ? service.testPrint : service.openDrawer;
    set({ activeCommand: command, error: null, lastCommand: { command, jobId, status: 'pending' } });

    try {
      let result;
      try {
        result = await execute(token, jobId);
      } catch (firstError) {
        if (firstError?.status) throw firstError;
        result = await execute(token, jobId);
      }

      set({ activeCommand: null, lastCommand: { command, jobId, ...result } });
      return result;
    } catch (error) {
      set({
        activeCommand: null,
        error: errorMessage(error),
        lastCommand: { command, jobId, status: 'failed' },
        ...(error?.status === 401 ? {
          token: null,
          agentId: null,
          agentPaired: false,
          pairedContext: null,
          printers: [],
          configurationSaved: false,
        } : {}),
      });
      throw error;
    }
  },

  testPrint: () => get().executeCommand('test-print'),
  openDrawer: () => get().executeCommand('open-drawer'),
});

const usePrintAgentStore = create(
  persist(createPrintAgentState(), {
    name: 'walos-print-agent',
    storage: createJSONStorage(() => sessionStorage),
    partialize: (state) => ({
      token: state.token,
      agentId: state.agentId,
      pairedContext: state.pairedContext,
      config: state.config,
      configurationSaved: state.configurationSaved,
    }),
  })
);

export default usePrintAgentStore;
