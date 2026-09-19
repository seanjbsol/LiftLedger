import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from './api';
import { useAuth } from './auth';
import type { BillingEntitlements } from './types';

type EntitlementsContextValue = {
  billing: BillingEntitlements | null;
  loading: boolean;
  isPro: boolean;
  canUseCertificates: boolean;
  canUseDefects: boolean;
  canUseClientPortal: boolean;
  refresh: () => Promise<void>;
};

const EntitlementsContext = createContext<EntitlementsContextValue | null>(null);

const fallback: BillingEntitlements = {
  productCode: 'LiftLedger',
  tenantId: '',
  status: 'none',
  hasAccess: false,
  planTier: 'Starter',
  isPro: false,
  canUseCertificates: false,
  canUseDefects: false,
  canUseClientPortal: false
};

export function EntitlementsProvider({ children }: { children: ReactNode }) {
  const { token } = useAuth();
  const [billing, setBilling] = useState<BillingEntitlements | null>(null);
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!token) {
      setBilling(null);
      return;
    }
    setLoading(true);
    try {
      setBilling(await api.entitlements());
    } catch {
      setBilling(null);
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const current = billing ?? fallback;
  const value = useMemo<EntitlementsContextValue>(
    () => ({
      billing,
      loading,
      isPro: current.isPro,
      canUseCertificates: current.canUseCertificates,
      canUseDefects: current.canUseDefects,
      canUseClientPortal: current.canUseClientPortal,
      refresh
    }),
    [billing, current.canUseCertificates, current.canUseClientPortal, current.canUseDefects, current.isPro, loading, refresh]
  );

  return <EntitlementsContext.Provider value={value}>{children}</EntitlementsContext.Provider>;
}

export function useEntitlements() {
  const ctx = useContext(EntitlementsContext);
  if (!ctx) {
    throw new Error('useEntitlements must be used within EntitlementsProvider');
  }
  return ctx;
}
