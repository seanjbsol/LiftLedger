import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setToken } from './api';
import type { AuthResponse, Role, TenantSummary, UserSummary } from './types';

const STORAGE_KEY = 'liftledger.session';

type Session = {
  token: string;
  user: UserSummary;
  tenant: TenantSummary;
  role: Role;
};

type AuthContextValue = Session & {
  ready: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: {
    organisationName: string;
    email: string;
    fullName: string;
    password: string;
    town?: string;
    postcode?: string;
  }) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

async function persist(session: Session | null) {
  setToken(session?.token ?? null);
  if (session) {
    await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } else {
    await AsyncStorage.removeItem(STORAGE_KEY);
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const raw = await AsyncStorage.getItem(STORAGE_KEY);
        if (raw) {
          const parsed = JSON.parse(raw) as Session;
          setToken(parsed.token);
          setSession(parsed);
        }
      } finally {
        setReady(true);
      }
    })();
  }, []);

  const apply = useCallback(async (response: AuthResponse) => {
    const next: Session = {
      token: response.token,
      user: response.user,
      tenant: response.tenant,
      role: response.role
    };
    await persist(next);
    setSession(next);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password);
    await apply(response);
  }, [apply]);

  const register = useCallback(async (input: Parameters<AuthContextValue['register']>[0]) => {
    const response = await api.register(input);
    await apply(response);
  }, [apply]);

  const signOut = useCallback(async () => {
    await persist(null);
    setSession(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      ready,
      token: session?.token ?? '',
      user: session?.user ?? { id: '', email: '', fullName: '' },
      tenant: session?.tenant ?? { id: '', name: '' },
      role: session?.role ?? 'Viewer',
      login,
      register,
      signOut
    }),
    [ready, session, login, register, signOut]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return ctx;
}
