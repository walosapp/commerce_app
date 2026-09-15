import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import toast from 'react-hot-toast';
import printService from '../services/printService';
import { createPostSaleHardwareRunner, hasCashPayment } from '../services/postSaleHardwareService';
import { createPrintReceiptCommand } from '../services/receiptDocument';
import usePrintAgentStore from './printAgentStore';

export const DEFAULT_POST_SALE_POLICY = Object.freeze({
  autoPrintReceipt: false,
  autoOpenCashDrawer: false,
});
const MAX_PERSISTED_INTENTS = 100;
const MAX_UNRESOLVED_WARNING = 100;
const POST_SALE_STORAGE_PROBE_KEY = 'walos-post-sale-hardware-probe';
export const POST_SALE_PERSISTENCE_WARNING =
  'El almacenamiento local no esta disponible. Las acciones fisicas postventa y las reimpresiones quedan bloqueadas hasta recargar con el almacenamiento recuperado.';

export const probePostSaleStorage = (storage) => {
  if (!storage) return false;
  try {
    storage.setItem(POST_SALE_STORAGE_PROBE_KEY, '1');
    const valid = storage.getItem(POST_SALE_STORAGE_PROBE_KEY) === '1';
    storage.removeItem(POST_SALE_STORAGE_PROBE_KEY);
    return valid;
  } catch {
    return false;
  }
};

export const createSafePostSaleStorage = (storage, onFailure = () => {}) => ({
  getItem: (name) => {
    try { return storage.getItem(name); } catch { onFailure(); return null; }
  },
  setItem: (name, value) => {
    try { storage.setItem(name, value); } catch { onFailure(); }
  },
  removeItem: (name) => {
    try { storage.removeItem(name); } catch { onFailure(); }
  },
});

const contextKey = (companyId, branchId) => `c${Number(companyId)}:b${Number(branchId)}`;
export const postSaleIntentKey = (companyId, branchId, orderId) =>
  `${contextKey(companyId, branchId)}:o${Number(orderId)}`;
export const isUnresolvedPostSalePrint = (intent) => Boolean(
  intent &&
  ['failed', 'uncertain', 'stale'].includes(intent.printStatus) &&
  !intent.printReviewed
);
export const isUnresolvedPostSaleDrawer = (intent) => Boolean(
  intent &&
  ['failed', 'uncertain', 'stale'].includes(intent.drawerStatus) &&
  !intent.drawerReviewed
);
const isTerminalPostSalePrint = (intent) => Boolean(
  intent && (
    intent.printReviewed === true ||
    ['completed', 'replayed', 'acknowledged'].includes(intent.printStatus) ||
    String(intent.printStatus || '').startsWith('skipped_')
  )
);
const isTerminalPostSaleDrawer = (intent) => Boolean(
  intent && (
    intent.drawerReviewed === true ||
    ['completed', 'replayed', 'acknowledged'].includes(intent.drawerStatus) ||
    String(intent.drawerStatus || '').startsWith('skipped_') ||
    (!intent.drawerStatus && !intent.drawerJobId)
  )
);
export const isBlockingPostSaleIntent = (intent) => Boolean(
  intent && (!isTerminalPostSalePrint(intent) || !isTerminalPostSaleDrawer(intent))
);
// Alias temporal para consumidores H3 que todavía usan el nombre anterior.
export const isBlockingPostSalePrint = isBlockingPostSaleIntent;
const normalizePolicy = (policy = {}) => ({
  autoPrintReceipt: Boolean(policy.autoPrintReceipt),
  autoOpenCashDrawer: Boolean(policy.autoOpenCashDrawer),
});
export const retainSafePostSaleIntents = (intents) => {
  const entries = Object.entries(intents);
  const blocking = entries.filter(([, intent]) => isBlockingPostSaleIntent(intent));
  const terminal = entries
    .filter(([, intent]) => !isBlockingPostSaleIntent(intent))
    .slice(-MAX_PERSISTED_INTENTS);
  return Object.fromEntries([...terminal, ...blocking]);
};
export const postSaleStorageWarningFor = (intents) => (
  Object.values(intents).filter(isBlockingPostSaleIntent).length > MAX_UNRESOLVED_WARNING
    ? 'Hay mas de 100 acciones fisicas postventa sin reconciliar. Revisalas antes de continuar; no se eliminaron automaticamente.'
    : null
);
const upsertIntentState = (current, key, intent, previousKey = null, persistenceFailed = false) => {
  const next = { ...current };
  if (previousKey) delete next[previousKey];
  delete next[key];
  next[key] = intent;
  const intents = retainSafePostSaleIntents(next);
  return {
    intents,
    storageWarning: persistenceFailed
      ? POST_SALE_PERSISTENCE_WARNING
      : postSaleStorageWarningFor(intents),
  };
};

const notifyOutcome = (outcome) => {
  const baseId = `post-sale-${outcome.orderId}`;
  if (outcome.status === 'fetch_failed') {
    toast.error('Venta guardada. No se pudo consultar el recibo; no se ejecuto ningun comando fisico.', { id: `${baseId}-fetch` });
    return;
  }
  if (outcome.status === 'receipt_ineligible') {
    toast.error('La orden ya no es elegible; no se imprimió ni se abrió el cajón.', { id: `${baseId}-eligibility` });
    return;
  }
  if (outcome.status === 'persistence_failed') {
    toast.error(POST_SALE_PERSISTENCE_WARNING, { id: `${baseId}-persistence` });
    return;
  }

  if (outcome.printStatus === 'completed') {
    toast.success('Recibo impreso', { id: `${baseId}-print` });
  } else if (outcome.printStatus === 'replayed') {
    toast.success('El recibo ya habia sido procesado; no se duplico.', { id: `${baseId}-print` });
  } else if (['failed', 'uncertain'].includes(outcome.printStatus)) {
    toast.error('No fue posible imprimir. La venta permanece registrada.', { id: `${baseId}-print` });
  }

  if (outcome.drawerStatus === 'completed') {
    toast.success('Cajón abierto', { id: `${baseId}-drawer` });
  } else if (outcome.drawerStatus === 'replayed') {
    toast.success('La apertura del cajon ya habia sido procesada; no se repitio.', { id: `${baseId}-drawer` });
  } else if (['failed', 'uncertain'].includes(outcome.drawerStatus)) {
    toast.error('No fue posible abrir el cajón. La venta permanece registrada.', { id: `${baseId}-drawer` });
  }
};

const persistedIntent = (intent) => {
  if (!intent) return intent;
  const printInterrupted = ['pending_policy', 'queued', 'retrying'].includes(intent.printStatus);
  const drawerInterrupted = ['pending_policy', 'queued', 'retrying'].includes(intent.drawerStatus);
  return {
    ...intent,
    ...(printInterrupted ? { printStatus: 'uncertain', printErrorCode: 'interrupted' } : {}),
    ...(drawerInterrupted ? { drawerStatus: 'uncertain', drawerErrorCode: 'interrupted' } : {}),
  };
};

export const selectPersistedPostSaleState = (state) => {
  const intents = Object.fromEntries(
    Object.entries(retainSafePostSaleIntents(state.intents)).map(([key, intent]) => [key, persistedIntent(intent)])
  );
  return {
    policies: state.policies,
    intents,
    lastOutcome: persistedIntent(state.lastOutcome),
    storageWarning: postSaleStorageWarningFor(intents),
  };
};

const defaultDependencies = {
  getReceipt: (orderId) => printService.getReceipt(orderId),
  printReceipt: (receipt, options) => usePrintAgentStore.getState().printPostSaleReceipt(receipt, options),
  openDrawer: (options) => usePrintAgentStore.getState().openDrawer(options),
  notify: notifyOutcome,
};

export const createPostSaleHardwareState = (dependencies = defaultDependencies, options = {}) => {
  const deps = { ...defaultDependencies, ...dependencies };
  const runner = createPostSaleHardwareRunner(deps);
  let queue = Promise.resolve();
  const inFlight = new Map();

  return (set, get) => ({
    policies: {},
    intents: {},
    lastOutcome: null,
    persistenceFailed: Boolean(options.persistenceFailed),
    storageWarning: options.persistenceFailed ? POST_SALE_PERSISTENCE_WARNING : null,

    reportPersistenceFailure: () => set({
      persistenceFailed: true,
      storageWarning: POST_SALE_PERSISTENCE_WARNING,
    }),

    getPolicy: (companyId, branchId) => (
      get().policies[contextKey(companyId, branchId)] || DEFAULT_POST_SALE_POLICY
    ),
    getIntent: (companyId, branchId, orderId) => {
      const exact = get().intents[postSaleIntentKey(companyId, branchId, orderId)];
      if (exact) return exact;
      return Object.values(get().intents).find((intent) => (
        Number(intent.orderId) === Number(orderId) &&
        (!intent.companyId || (
          Number(intent.companyId) === Number(companyId) &&
          Number(intent.branchId) === Number(branchId)
        ))
      )) || null;
    },

    updatePolicy: ({ companyId, branchId }, partial) => set((state) => {
      const key = contextKey(companyId, branchId);
      const current = state.policies[key] || DEFAULT_POST_SALE_POLICY;
      return {
        policies: {
          ...state.policies,
          [key]: normalizePolicy({
            ...current,
        ...(partial.autoPrintReceipt === undefined
          ? {}
          : { autoPrintReceipt: Boolean(partial.autoPrintReceipt) }),
        ...(partial.autoOpenCashDrawer === undefined
          ? {}
          : { autoOpenCashDrawer: Boolean(partial.autoOpenCashDrawer) }),
          }),
        },
      };
    }),

    enqueuePostSale: ({ orderId }) => {
      if (get().persistenceFailed) {
        const blocked = {
          orderId: Number(orderId),
          status: 'persistence_failed',
          printStatus: 'skipped_persistence_unavailable',
          drawerStatus: 'skipped_persistence_unavailable',
        };
        try { deps.notify(blocked); } catch { /* la venta ya fue persistida */ }
        return Promise.resolve(blocked);
      }
      const inFlightKey = String(orderId);
      if (inFlight.has(inFlightKey)) return inFlight.get(inFlightKey);
      const existingBlocking = Object.values(get().intents).find((intent) => (
        Number(intent.orderId) === Number(orderId) && isBlockingPostSaleIntent(intent)
      ));
      if (existingBlocking) return Promise.resolve(existingBlocking);
      let key = `pending:o${Number(orderId)}`;

      set((state) => upsertIntentState(
        state.intents,
        key,
        {
          orderId: Number(orderId),
          status: 'queued',
          printStatus: 'pending_policy',
          drawerStatus: 'pending_policy',
        },
        null,
        state.persistenceFailed
      ));
      if (get().persistenceFailed) {
        const blocked = {
          ...get().intents[key],
          status: 'persistence_failed',
        };
        try { deps.notify(blocked); } catch { /* la venta ya fue persistida */ }
        return Promise.resolve(blocked);
      }

      const task = queue.then(async () => {
        let outcome;
        try {
          outcome = await runner({
            orderId,
            resolvePolicy: (companyId, branchId) => get().getPolicy(companyId, branchId),
            onProgress: (progress) => {
              const nextKey = progress.companyId && progress.branchId
                ? postSaleIntentKey(progress.companyId, progress.branchId, progress.orderId)
                : key;
              set((state) => upsertIntentState(
                state.intents,
                nextKey,
                progress,
                nextKey !== key ? key : null,
                state.persistenceFailed
              ));
              key = nextKey;
            },
          });
        } catch (error) {
          outcome = {
            orderId: Number(orderId),
            status: 'invalid_request',
            printStatus: 'skipped_invalid_request',
            drawerStatus: 'skipped_invalid_request',
            errorCode: String(error?.code || 'invalid_post_sale_request').slice(0, 100),
          };
          set((state) => upsertIntentState(
            state.intents,
            key,
            outcome,
            null,
            state.persistenceFailed
          ));
        }
        set({ lastOutcome: outcome });
        try {
          deps.notify(outcome);
        } catch {
          // La notificacion visual nunca altera el resultado postventa.
        }
        return outcome;
      });

      queue = task.catch(() => {});
      inFlight.set(inFlightKey, task);
      task.finally(() => inFlight.delete(inFlightKey)).catch(() => {});
      return task;
    },

    retryPostSalePrint: async ({ companyId, branchId, orderId }) => {
      if (get().persistenceFailed) {
        throw Object.assign(new Error(POST_SALE_PERSISTENCE_WARNING), { code: 'persistence_unavailable' });
      }
      const key = postSaleIntentKey(companyId, branchId, orderId);
      const intent = get().intents[key];
      if (!isUnresolvedPostSalePrint(intent) || !intent.receiptJobId || !intent.printFingerprint) {
        throw new Error('No hay un intento postventa incierto para reconciliar.');
      }

      set((state) => upsertIntentState(
        state.intents,
        key,
        { ...intent, printStatus: 'retrying' },
        null,
        state.persistenceFailed
      ));
      if (get().persistenceFailed) {
        throw Object.assign(new Error(POST_SALE_PERSISTENCE_WARNING), { code: 'persistence_unavailable' });
      }

      let receipt;
      let command;
      try {
        const response = await deps.getReceipt(Number(orderId));
        receipt = response?.data;
        command = await createPrintReceiptCommand(receipt, intent.receiptJobId);
      } catch (error) {
        const unresolved = {
          ...intent,
          printStatus: 'uncertain',
          printErrorCode: String(error?.code || 'receipt_unavailable').slice(0, 100),
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, unresolved, null, state.persistenceFailed),
          lastOutcome: unresolved,
        }));
        throw error;
      }
      const changed = command.companyId !== Number(companyId) ||
        command.branchId !== Number(branchId) ||
        command.orderId !== Number(orderId) ||
        command.fingerprint !== intent.printFingerprint;
      if (changed) {
        const stale = {
          ...intent,
          printStatus: 'stale',
          printErrorCode: 'receipt_changed',
          printReviewed: false,
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, stale, null, state.persistenceFailed),
          lastOutcome: stale,
        }));
        throw Object.assign(
          new Error('El recibo persistido cambió. Revisá físicamente el intento antes de crear otro trabajo.'),
          { code: 'receipt_changed' }
        );
      }

      try {
        const result = await deps.printReceipt(receipt, { jobId: intent.receiptJobId });
        const resolved = {
          ...intent,
          printStatus: ['completed', 'replayed'].includes(result?.status) ? result.status : 'uncertain',
          printErrorCode: null,
          printReviewed: false,
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, resolved, null, state.persistenceFailed),
          lastOutcome: resolved,
        }));
        return result;
      } catch (error) {
        const status = Number(error?.status);
        const failed = Number.isInteger(status) && status >= 400 && status < 500;
        const unresolved = {
          ...intent,
          printStatus: failed ? 'failed' : 'uncertain',
          printErrorCode: String(error?.code || 'hardware_error').slice(0, 100),
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, unresolved, null, state.persistenceFailed),
          lastOutcome: unresolved,
        }));
        throw error;
      }
    },

    retryPostSaleDrawer: async ({ companyId, branchId, orderId }) => {
      if (get().persistenceFailed) {
        throw Object.assign(new Error(POST_SALE_PERSISTENCE_WARNING), { code: 'persistence_unavailable' });
      }
      const key = postSaleIntentKey(companyId, branchId, orderId);
      const intent = get().intents[key];
      if (!isUnresolvedPostSaleDrawer(intent) || !intent.drawerJobId) {
        throw new Error('No hay una apertura de cajon incierta para reconciliar.');
      }

      set((state) => upsertIntentState(
        state.intents,
        key,
        { ...intent, drawerStatus: 'retrying' },
        null,
        state.persistenceFailed
      ));
      if (get().persistenceFailed) {
        throw Object.assign(new Error(POST_SALE_PERSISTENCE_WARNING), { code: 'persistence_unavailable' });
      }

      let receipt;
      try {
        const response = await deps.getReceipt(Number(orderId));
        receipt = response?.data;
      } catch (error) {
        const unresolved = {
          ...intent,
          drawerStatus: 'uncertain',
          drawerErrorCode: String(error?.code || 'receipt_unavailable').slice(0, 100),
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, unresolved, null, state.persistenceFailed),
          lastOutcome: unresolved,
        }));
        throw error;
      }

      const eligible = receipt &&
        Number(receipt.orderId) === Number(orderId) &&
        Number(receipt.companyId) === Number(companyId) &&
        Number(receipt.branchId) === Number(branchId) &&
        receipt.status === 'completed' &&
        !(typeof receipt.refundStatus === 'string' && receipt.refundStatus.trim()) &&
        hasCashPayment(receipt.payments);
      if (!eligible) {
        const stale = {
          ...intent,
          drawerStatus: 'stale',
          drawerErrorCode: 'receipt_ineligible',
          drawerReviewed: false,
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, stale, null, state.persistenceFailed),
          lastOutcome: stale,
        }));
        throw Object.assign(
          new Error('La orden ya no es elegible para abrir el cajon. Revisala antes de continuar.'),
          { code: 'receipt_ineligible' }
        );
      }

      try {
        const result = await deps.openDrawer({
          jobId: intent.drawerJobId,
          companyId: Number(companyId),
          branchId: Number(branchId),
        });
        const resolved = {
          ...intent,
          drawerStatus: ['completed', 'replayed'].includes(result?.status) ? result.status : 'uncertain',
          drawerErrorCode: null,
          drawerReviewed: false,
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, resolved, null, state.persistenceFailed),
          lastOutcome: resolved,
        }));
        return result;
      } catch (error) {
        const status = Number(error?.status);
        const failed = Number.isInteger(status) && status >= 400 && status < 500;
        const unresolved = {
          ...intent,
          drawerStatus: failed ? 'failed' : 'uncertain',
          drawerErrorCode: String(error?.code || 'hardware_error').slice(0, 100),
        };
        set((state) => ({
          ...upsertIntentState(state.intents, key, unresolved, null, state.persistenceFailed),
          lastOutcome: unresolved,
        }));
        throw error;
      }
    },

    acknowledgePostSalePrint: ({ companyId, branchId, orderId }) => {
      const key = postSaleIntentKey(companyId, branchId, orderId);
      set((state) => {
        const intent = state.intents[key];
        if (!isUnresolvedPostSalePrint(intent)) return state;
        const reviewed = { ...intent, printReviewed: true };
        return {
          ...upsertIntentState(state.intents, key, reviewed, null, state.persistenceFailed),
          lastOutcome: reviewed,
        };
      });
    },

    acknowledgePostSaleDrawer: ({ companyId, branchId, orderId }) => {
      const key = postSaleIntentKey(companyId, branchId, orderId);
      set((state) => {
        const intent = state.intents[key];
        if (!isUnresolvedPostSaleDrawer(intent)) return state;
        const reviewed = { ...intent, drawerReviewed: true };
        return {
          ...upsertIntentState(state.intents, key, reviewed, null, state.persistenceFailed),
          lastOutcome: reviewed,
        };
      });
    },
  });
};

const browserStorage = typeof localStorage === 'undefined' ? null : localStorage;
const persistenceAvailableAtStartup = probePostSaleStorage(browserStorage);
let persistenceFailureBeforeInitialization = false;
let reportPersistenceFailure = () => { persistenceFailureBeforeInitialization = true; };
let persistenceFailureReported = false;
const safeBrowserStorage = createSafePostSaleStorage(browserStorage, () => reportPersistenceFailure());

const usePostSaleHardwareStore = create(
  persist(createPostSaleHardwareState(defaultDependencies, {
    persistenceFailed: !persistenceAvailableAtStartup,
  }), {
    name: 'walos-post-sale-hardware',
    storage: createJSONStorage(() => safeBrowserStorage),
    partialize: selectPersistedPostSaleState,
    version: 1,
  })
);

reportPersistenceFailure = () => {
  if (persistenceFailureReported) return;
  persistenceFailureReported = true;
  usePostSaleHardwareStore.getState().reportPersistenceFailure();
};
if (persistenceFailureBeforeInitialization) reportPersistenceFailure();

export default usePostSaleHardwareStore;
