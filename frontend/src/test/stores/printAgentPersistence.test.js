import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import usePrintAgentStore from '../../stores/printAgentStore';

const persistedState = {
  token: 'persistent-agent-token',
  agentId: 'agent-1',
  pairedContext: { companyId: 25, branchId: 7, workstationId: 'POS-1' },
  config: {
    companyId: 25,
    branchId: 7,
    workstationId: 'POS-1',
    printerName: 'POS-58',
    drawerPin: 0,
    drawerOnTimeMs: 120,
    drawerOffTimeMs: 240,
  },
  configurationSaved: true,
  lastCommand: null,
  pendingReceiptCommand: null,
};

describe('printAgentStore persistence', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    usePrintAgentStore.setState({ token: null, pairedContext: null, configurationSaved: false });
  });

  it('rehidrata el pairing de la estación desde localStorage', async () => {
    localStorage.setItem('walos-print-agent', JSON.stringify({ state: persistedState, version: 0 }));
    sessionStorage.setItem('walos-print-agent', JSON.stringify({
      state: { ...persistedState, token: 'obsolete-session-token' },
      version: 0,
    }));

    await usePrintAgentStore.persist.rehydrate();

    expect(usePrintAgentStore.getState()).toMatchObject({
      token: 'persistent-agent-token',
      pairedContext: persistedState.pairedContext,
      config: persistedState.config,
      configurationSaved: true,
    });
  });
});
