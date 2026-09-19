import type {
  AssetDetail,
  AssetSummary,
  AuthResponse,
  BillingEntitlements,
  BillingSession,
  DashboardResponse,
  DefectInput,
  InspectionDetail,
  InspectionSummary,
  InspectionResult,
  ExaminationType
} from './types';

export const API_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

let authToken: string | null = null;

export function setToken(token: string | null) {
  authToken = token;
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }
  if (authToken) {
    headers.set('Authorization', `Bearer ${authToken}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(0, 'Could not reach the LiftLedger API. Check EXPO_PUBLIC_API_URL and your connection.');
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const message = data?.title ?? data?.detail ?? `Request failed (${response.status})`;
    throw new ApiError(response.status, message);
  }
  return data as T;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password })
    }),
  register: (body: Record<string, string | undefined>) =>
    request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(body)
    }),
  dashboard: () => request<DashboardResponse>('/api/dashboard'),
  assets: (q?: string) => request<AssetSummary[]>(`/api/assets${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  asset: (id: string) => request<AssetDetail>(`/api/assets/${id}`),
  createAsset: (body: Record<string, unknown>) =>
    request<AssetDetail>('/api/assets', { method: 'POST', body: JSON.stringify(body) }),
  inspections: (assetId?: string) =>
    request<InspectionSummary[]>(`/api/inspections${assetId ? `?assetId=${assetId}` : ''}`),
  inspection: (id: string) => request<InspectionDetail>(`/api/inspections/${id}`),
  startInspection: (assetId: string, examinationType: ExaminationType = 'ThoroughExamination') =>
    request<InspectionDetail>('/api/inspections', {
      method: 'POST',
      body: JSON.stringify({ assetId, examinationType })
    }),
  completeInspection: (
    id: string,
    body: {
      result: InspectionResult;
      nextDueDate: string;
      examinationDate?: string;
      safeWorkingLoad?: string;
      particularsOfExamination?: string;
      particularsOfDefects?: string;
      repairsRequired?: string;
      examinerQualifications?: string;
      defects: DefectInput[];
    }
  ) =>
    request<InspectionDetail>(`/api/inspections/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify(body)
    }),
  certificateUrl: (id: string) => `${API_URL}/api/inspections/${id}/certificate`,
  entitlements: () => request<BillingEntitlements>('/api/billing/entitlements'),
  checkout: () =>
    request<BillingSession>('/api/billing/checkout', { method: 'POST', body: JSON.stringify({}) }),
  portal: () =>
    request<BillingSession>('/api/billing/portal', { method: 'POST', body: JSON.stringify({}) })
};
