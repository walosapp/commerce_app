import { useCallback, useEffect, useRef, useState } from 'react';

const INITIAL_STATE = {
  weight: 0,
  isStable: false,
  isConnected: false,
  error: null,
};

const parseScaleLine = (line) => {
  const normalized = line.trim();
  if (!normalized) return null;

  const stable = normalized.startsWith('ST');
  const match = normalized.match(/([-+]?\d+(?:[.,]\d+)?)/);
  if (!match) return null;

  return {
    stable,
    weight: Number.parseFloat(match[1].replace(',', '.')) || 0,
  };
};

const useScale = () => {
  const [state, setState] = useState(INITIAL_STATE);
  const portRef = useRef(null);
  const readerRef = useRef(null);
  const keepReadingRef = useRef(false);

  const disconnect = useCallback(async () => {
    keepReadingRef.current = false;

    try {
      await readerRef.current?.cancel();
    } catch {}

    try {
      await portRef.current?.close();
    } catch {}

    readerRef.current = null;
    portRef.current = null;
    setState((prev) => ({ ...prev, isConnected: false }));
  }, []);

  const connect = useCallback(async () => {
    if (!('serial' in navigator)) {
      setState((prev) => ({
        ...prev,
        error: 'Web Serial API no está disponible en este navegador. Usá Chrome o Edge.',
      }));
      return;
    }

    try {
      const port = await navigator.serial.requestPort();
      await port.open({
        baudRate: 9600,
        dataBits: 8,
        stopBits: 1,
        parity: 'none',
      });

      portRef.current = port;
      keepReadingRef.current = true;
      setState((prev) => ({ ...prev, isConnected: true, error: null }));

      const decoder = new TextDecoder();
      let buffer = '';
      const reader = port.readable.getReader();
      readerRef.current = reader;

      while (keepReadingRef.current) {
        const { value, done } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() || '';

        for (const line of lines) {
          const parsed = parseScaleLine(line);
          if (!parsed) continue;

          setState((prev) => ({
            ...prev,
            weight: parsed.weight,
            isStable: parsed.stable,
            isConnected: true,
            error: null,
          }));
        }
      }
    } catch (error) {
      setState((prev) => ({
        ...prev,
        error: error?.message || 'No se pudo conectar la báscula',
        isConnected: false,
      }));
    }
  }, []);

  useEffect(() => () => {
    disconnect();
  }, [disconnect]);

  return {
    ...state,
    connect,
    disconnect,
  };
};

export default useScale;
