import { useEffect, useMemo, useState } from 'react';

const isStandaloneMode = () =>
  window.matchMedia?.('(display-mode: standalone)').matches || window.navigator.standalone === true;

export const usePwaInstall = () => {
  const [deferredPrompt, setDeferredPrompt] = useState(null);
  const [isInstalled, setIsInstalled] = useState(isStandaloneMode());

  useEffect(() => {
    const handleBeforeInstallPrompt = (event) => {
      event.preventDefault();
      setDeferredPrompt(event);
    };

    const handleInstalled = () => {
      setDeferredPrompt(null);
      setIsInstalled(true);
    };

    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
    window.addEventListener('appinstalled', handleInstalled);

    return () => {
      window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
      window.removeEventListener('appinstalled', handleInstalled);
    };
  }, []);

  const canInstall = useMemo(() => !!deferredPrompt && !isInstalled, [deferredPrompt, isInstalled]);

  const promptInstall = async () => {
    if (!deferredPrompt) {
      return { outcome: 'unavailable' };
    }

    await deferredPrompt.prompt();
    const choice = await deferredPrompt.userChoice;
    setDeferredPrompt(null);

    if (choice.outcome === 'accepted') {
      setIsInstalled(true);
    }

    return choice;
  };

  return {
    canInstall,
    isInstalled,
    promptInstall,
  };
};
