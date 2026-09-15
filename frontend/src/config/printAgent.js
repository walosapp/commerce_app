const LOOPBACK_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]']);

export const resolvePrintAgentDownloadUrl = (
  rawUrl = import.meta.env.VITE_WALOS_AGENT_DOWNLOAD_URL,
  { isDevelopment = import.meta.env.DEV } = {}
) => {
  const candidate = String(rawUrl || '').trim();
  if (!candidate) return null;

  try {
    const parsed = new URL(candidate);
    if (parsed.username || parsed.password) return null;

    const isSecureRelease = parsed.protocol === 'https:';
    const isLoopbackDevelopment = isDevelopment
      && parsed.protocol === 'http:'
      && LOOPBACK_HOSTS.has(parsed.hostname);
    if (!isSecureRelease && !isLoopbackDevelopment) return null;

    return parsed.href;
  } catch {
    return null;
  }
};
