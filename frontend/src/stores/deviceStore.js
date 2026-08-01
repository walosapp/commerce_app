import { create } from 'zustand';
import { persist } from 'zustand/middleware';

const DEFAULT_SCALE_CONFIG = {
  deviceType: 'scale',
  connectionType: 'serial',
  label: 'Bascula principal',
  baudRate: 9600,
  dataBits: 8,
  stopBits: 1,
  parity: 'none',
  weightUnit: 'kg',
};

const INITIAL_SCALE_STATE = {
  weight: 0,
  isStable: false,
  isConnected: false,
  isConnecting: false,
  error: null,
  lastReadingAt: null,
  lastRawLine: null,
};

const runtime = {
  port: null,
  reader: null,
  keepReading: false,
};

const parseScaleLine = (line) => {
  const normalized = line.trim();
  if (!normalized) return null;

  const stable = normalized.startsWith('ST');
  const match = normalized.match(/([-+]?\d+(?:[.,]\d+)?)/);
  if (!match) return null;

  return {
    stable,
    raw: normalized,
    weight: Number.parseFloat(match[1].replace(',', '.')) || 0,
  };
};

const closeRuntimeConnection = async () => {
  runtime.keepReading = false;

  try {
    await runtime.reader?.cancel();
  } catch {}

  try {
    runtime.reader?.releaseLock?.();
  } catch {}

  try {
    await runtime.port?.close();
  } catch {}

  runtime.reader = null;
  runtime.port = null;
};

const useDeviceStore = create(
  persist(
    (set, get) => ({
      selectedDeviceType: 'scale',
      scaleConfig: DEFAULT_SCALE_CONFIG,
      scale: INITIAL_SCALE_STATE,

      setSelectedDeviceType: (selectedDeviceType) => set({ selectedDeviceType }),

      updateScaleConfig: (partial) =>
        set((state) => ({
          scaleConfig: {
            ...state.scaleConfig,
            ...partial,
          },
        })),

      clearScaleError: () =>
        set((state) => ({
          scale: {
            ...state.scale,
            error: null,
          },
        })),

      disconnectScale: async () => {
        await closeRuntimeConnection();
        set({
          scale: {
            ...INITIAL_SCALE_STATE,
          },
        });
      },

      connectScale: async () => {
        if (typeof navigator === 'undefined' || !('serial' in navigator)) {
          set((state) => ({
            scale: {
              ...state.scale,
              isConnected: false,
              isConnecting: false,
              error: 'Web Serial API no esta disponible en este navegador. Usa Chrome o Edge.',
            },
          }));
          return;
        }

        const { scaleConfig } = get();

        set((state) => ({
          scale: {
            ...state.scale,
            isConnecting: true,
            error: null,
          },
        }));

        try {
          if (runtime.port) {
            await get().disconnectScale();
          }

          const port = await navigator.serial.requestPort();
          await port.open({
            baudRate: Number(scaleConfig.baudRate) || 9600,
            dataBits: Number(scaleConfig.dataBits) || 8,
            stopBits: Number(scaleConfig.stopBits) || 1,
            parity: scaleConfig.parity || 'none',
          });

          runtime.port = port;
          runtime.keepReading = true;

          set((state) => ({
            scale: {
              ...state.scale,
              isConnected: true,
              isConnecting: false,
              error: null,
            },
          }));

          const decoder = new TextDecoder();
          let buffer = '';
          const reader = port.readable.getReader();
          runtime.reader = reader;

          while (runtime.keepReading) {
            const { value, done } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split(/\r?\n/);
            buffer = lines.pop() || '';

            for (const line of lines) {
              const parsed = parseScaleLine(line);
              if (!parsed) continue;

              set((state) => ({
                scale: {
                  ...state.scale,
                  weight: parsed.weight,
                  isStable: parsed.stable,
                  isConnected: true,
                  isConnecting: false,
                  error: null,
                  lastRawLine: parsed.raw,
                  lastReadingAt: new Date().toISOString(),
                },
              }));
            }
          }

          if (runtime.keepReading) {
            await closeRuntimeConnection();
            set((state) => ({
              scale: {
                ...state.scale,
                isConnected: false,
                isConnecting: false,
              },
            }));
          }
        } catch (error) {
          await closeRuntimeConnection();
          set((state) => ({
            scale: {
              ...state.scale,
              isConnected: false,
              isConnecting: false,
              error: error?.message || 'No se pudo conectar la bascula',
            },
          }));
        }
      },
    }),
    {
      name: 'device-settings-storage',
      partialize: (state) => ({
        selectedDeviceType: state.selectedDeviceType,
        scaleConfig: state.scaleConfig,
      }),
    }
  )
);

export default useDeviceStore;
