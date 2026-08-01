import { useEffect, useRef, useCallback } from 'react';

const DEFAULT_OPTIONS = {
  minLength: 6,
  maxDelay: 50,
  onScan: null,
};

const useBarcodeScanner = (options = {}) => {
  const { minLength, maxDelay, onScan } = { ...DEFAULT_OPTIONS, ...options };
  const bufferRef = useRef('');
  const lastKeyTimeRef = useRef(0);
  const timeoutRef = useRef(null);
  const lastBarcodeRef = useRef(null);

  const processBuffer = useCallback(() => {
    const code = bufferRef.current.trim();
    if (code.length >= minLength) {
      lastBarcodeRef.current = code;
      onScan?.(code);
    }
    bufferRef.current = '';
  }, [minLength, onScan]);

  useEffect(() => {
    const handleKeyDown = (e) => {
      const target = e.target;
      const isEditableTarget =
        target instanceof HTMLElement &&
        (
          target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.isContentEditable
        );

      if (isEditableTarget && !target.hasAttribute('data-barcode-allowed')) {
        bufferRef.current = '';
        clearTimeout(timeoutRef.current);
        return;
      }

      const now = Date.now();
      const timeSinceLastKey = now - lastKeyTimeRef.current;

      if (e.key === 'Enter') {
        if (bufferRef.current.length >= minLength) {
          e.preventDefault();
          e.stopPropagation();
          processBuffer();
        }
        bufferRef.current = '';
        clearTimeout(timeoutRef.current);
        return;
      }

      if (e.key.length === 1 && !e.ctrlKey && !e.altKey && !e.metaKey) {
        if (timeSinceLastKey > maxDelay && bufferRef.current.length > 0) {
          bufferRef.current = '';
        }

        bufferRef.current += e.key;
        lastKeyTimeRef.current = now;

        clearTimeout(timeoutRef.current);
        timeoutRef.current = setTimeout(() => {
          bufferRef.current = '';
        }, maxDelay * 3);
      }
    };

    window.addEventListener('keydown', handleKeyDown, true);
    return () => {
      window.removeEventListener('keydown', handleKeyDown, true);
      clearTimeout(timeoutRef.current);
    };
  }, [maxDelay, minLength, processBuffer]);

  return { lastBarcode: lastBarcodeRef.current };
};

export default useBarcodeScanner;
