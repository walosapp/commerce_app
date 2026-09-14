import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import printAgentService from '../services/printAgentService';
import { createPrintReceiptCommand } from '../services/receiptDocument';

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
const isDeterministicClientError = (error) => {
  const status = Number(error?.status);
  return Number.isInteger(status) && status >= 400 && status < 500;
};

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
  pendingReceiptCommand: null,
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
        ...(contextChanged ? {
          token: null,
          agentId: null,
          agentPaired: false,
          printers: [],
          configurationSaved: false,
        } : {}),
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

  printReceipt: async (receipt) => {
    const { token, configurationSaved, activeCommand } = get();
    if (!token) throw new Error('Vincula este navegador con el agente antes de imprimir.');
    if (!configurationSaved) throw new Error('Guarda la configuracion de impresora antes de imprimir.');
    if (activeCommand) throw new Error('Ya hay un comando de impresion en proceso.');

    const jobId = createJobId();
    set({
      activeCommand: 'print-receipt',
      error: null,
      lastCommand: { command: 'print-receipt', jobId, status: 'pending' },
    });

    let command;
    try {
      command = await createPrintReceiptCommand(receipt, jobId);
      set({
        lastCommand: { command: 'print-receipt', jobId, fingerprint: command.fingerprint, status: 'pending' },
        pendingReceiptCommand: command,
      });

      let result;
      try {
        result = await service.printReceipt(token, command);
      } catch (firstError) {
        if (firstError?.status) throw firstError;
        // El retry de transporte conserva exactamente jobId y fingerprint.
        result = await service.printReceipt(token, command);
      }

      set({
        activeCommand: null,
        lastCommand: { command: 'print-receipt', jobId, fingerprint: command.fingerprint, ...result },
        pendingReceiptCommand: result?.status === 'completed' || result?.status === 'replayed'
          ? null
          : command,
      });
      return result;
    } catch (error) {
      set({
        activeCommand: null,
        error: errorMessage(error),
        lastCommand: {
          command: 'print-receipt',
          jobId,
          fingerprint: command?.fingerprint,
          status: error?.status ? 'failed' : 'uncertain',
        },
        // Un 5xx puede ocurrir despues de que el spooler haya recibido el trabajo.
        // Solo un 4xx de preflight confirma que el agente no lo ejecuto.
        pendingReceiptCommand: isDeterministicClientError(error) ? null : command,
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

  retryReceipt: async (currentReceipt) => {
    const { token, configurationSaved, activeCommand, pendingReceiptCommand } = get();
    if (!token) throw new Error('Vincula este navegador con el agente antes de reintentar.');
    if (!configurationSaved) throw new Error('Guarda la configuracion de impresora antes de reintentar.');
    if (activeCommand) throw new Error('Ya hay un comando de impresion en proceso.');
    if (!pendingReceiptCommand) throw new Error('No hay un trabajo de recibo incierto para reintentar.');

    const { jobId, fingerprint } = pendingReceiptCommand;
    const currentCommand = await createPrintReceiptCommand(currentReceipt, jobId);
    const receiptChanged = currentCommand.companyId !== pendingReceiptCommand.companyId ||
      currentCommand.branchId !== pendingReceiptCommand.branchId ||
      currentCommand.orderId !== pendingReceiptCommand.orderId ||
      currentCommand.fingerprint !== fingerprint;
    if (receiptChanged) {
      const changedError = Object.assign(
        new Error('El recibo persistido cambió desde el intento anterior. Revisá el resultado físico antes de descartarlo.'),
        { code: 'receipt_changed' }
      );
      set({
        error: changedError.message,
        lastCommand: { command: 'print-receipt', jobId, fingerprint, status: 'stale' },
      });
      throw changedError;
    }

    set({
      activeCommand: 'print-receipt',
      error: null,
      lastCommand: { command: 'print-receipt', jobId, fingerprint, status: 'pending' },
    });

    try {
      // Se envía el documento reconstruido desde el ReceiptData actual, nunca el payload viejo.
      const result = await service.printReceipt(token, currentCommand);
      set({
        activeCommand: null,
        lastCommand: { command: 'print-receipt', jobId, fingerprint, ...result },
        pendingReceiptCommand: result?.status === 'completed' || result?.status === 'replayed'
          ? null
          : currentCommand,
      });
      return result;
    } catch (error) {
      set({
        activeCommand: null,
        error: errorMessage(error),
        lastCommand: {
          command: 'print-receipt',
          jobId,
          fingerprint,
          status: error?.status ? 'failed' : 'uncertain',
        },
        pendingReceiptCommand: isDeterministicClientError(error) ? null : currentCommand,
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

  acknowledgeReceiptAttempt: () => set((state) => {
    if (!state.pendingReceiptCommand) return state;

    return {
      pendingReceiptCommand: null,
      lastCommand: state.lastCommand
        ? { ...state.lastCommand, reviewed: true }
        : null,
      error: null,
    };
  }),

  testPrint: () => get().executeCommand('test-print'),
  openDrawer: () => get().executeCommand('open-drawer'),
});

export const selectPersistedPrintAgentState = (state) => ({
  token: state.token,
  agentId: state.agentId,
  pairedContext: state.pairedContext,
  config: state.config,
  configurationSaved: state.configurationSaved,
  lastCommand: state.lastCommand,
  pendingReceiptCommand: state.pendingReceiptCommand,
});

const usePrintAgentStore = create(
  persist(createPrintAgentState(), {
    name: 'walos-print-agent',
    storage: createJSONStorage(() => sessionStorage),
    partialize: selectPersistedPrintAgentState,
  })
);

export default usePrintAgentStore;
