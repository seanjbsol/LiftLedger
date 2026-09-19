import type {
  AssetDetail,
  AssetScan,
  AssetSummary,
  AuthResponse,
  BillingEntitlements,
  BillingSession,
  CertificateSummary,
  ClientDto,
  CompleteInspectionPayload,
  DashboardResponse,
  DefectDto,
  DefectPhotoKind,
  DefectStatus,
  ExaminationType,
  InspectionDetail,
  InspectionSummary,
  MemberDto,
  PortalToken,
  PuwerTemplate
} from './types';

export const API_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

let authToken: string | null = null;

export function setToken(token: string | null) {
  authToken = token;
}

export class ApiError extends Error {
  status: number;
  feature?: string;
  constructor(status: number, message: string, feature?: string) {
    super(message);
    this.status = status;
    this.feature = feature;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
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
  const data = text ? safeJson(text) : null;
  if (!response.ok) {
    const message = data?.title ?? data?.detail ?? `Request failed (${response.status})`;
    throw new ApiError(response.status, message, data?.feature);
  }
  return data as T;
}

function safeJson(text: string) {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function authorizedBlob(path: string, accept: string) {
  const headers = new Headers({ Accept: accept });
  if (authToken) {
    headers.set('Authorization', `Bearer ${authToken}`);
  }
  const response = await fetch(`${API_URL}${path}`, { headers });
  if (!response.ok) {
    const text = await response.text();
    const data = text ? safeJson(text) : null;
    throw new ApiError(response.status, data?.title ?? `Request failed (${response.status})`, data?.feature);
  }
  return response;
}

export async function openHtmlPath(path: string) {
  const response = await authorizedBlob(path, 'text/html');
  const html = await response.text();
  if (typeof window !== 'undefined') {
    const blob = new Blob([html], { type: 'text/html' });
    window.open(URL.createObjectURL(blob), '_blank');
    return;
  }
  throw new ApiError(0, 'Open this working record on the web client to view HTML.');
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
  members: () => request<MemberDto[]>('/api/auth/members'),
  dashboard: () => request<DashboardResponse>('/api/dashboard'),
  assets: (q?: string) => request<AssetSummary[]>(`/api/assets${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  asset: (id: string) => request<AssetDetail>(`/api/assets/${id}`),
  assetByCode: (code: string) => request<AssetScan>(`/api/assets/by-code/${encodeURIComponent(code)}`),
  qrUrl: (id: string) => `${API_URL}/api/assets/${id}/qr`,
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
  completeInspection: (id: string, body: CompleteInspectionPayload) =>
    request<InspectionDetail>(`/api/inspections/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify(body)
    }),
  raiseDefect: (inspectionId: string, body: Record<string, unknown>) =>
    request<DefectDto>(`/api/inspections/${inspectionId}/defects`, {
      method: 'POST',
      body: JSON.stringify(body)
    }),
  openInspectionCertificate: (id: string) => openHtmlPath(`/api/inspections/${id}/certificate`),
  openStoredCertificate: (id: string) => openHtmlPath(`/api/certificates/${id}/html`),
  certificates: () => request<CertificateSummary[]>('/api/certificates'),
  puwerTemplate: (category: string) => request<PuwerTemplate>(`/api/puwer/templates/${category}`),
  defects: (status?: DefectStatus) =>
    request<DefectDto[]>(`/api/defects${status ? `?status=${status}` : ''}`),
  defect: (id: string) => request<DefectDto>(`/api/defects/${id}`),
  assignDefect: (id: string, assignedToUserId: string) =>
    request<DefectDto>(`/api/defects/${id}/assign`, {
      method: 'POST',
      body: JSON.stringify({ assignedToUserId })
    }),
  closeDefect: (id: string, body: { closeNotes?: string; requiresRetest?: boolean }) =>
    request<DefectDto>(`/api/defects/${id}/close`, {
      method: 'POST',
      body: JSON.stringify(body)
    }),
  startRetest: (id: string) =>
    request<InspectionDetail>(`/api/defects/${id}/retest`, { method: 'POST', body: JSON.stringify({}) }),
  uploadDefectPhoto: async (id: string, kind: DefectPhotoKind, file: File | Blob, fileName: string) => {
    const form = new FormData();
    form.append('kind', kind);
    form.append('file', file, fileName);
    return request<DefectDto>(`/api/defects/${id}/photos`, { method: 'POST', body: form });
  },
  photoUrl: (defectId: string, photoId: string) => `${API_URL}/api/defects/${defectId}/photos/${photoId}`,
  clients: () => request<ClientDto[]>('/api/clients'),
  portalCertificates: () => request<CertificateSummary[]>('/api/portal'),
  createPortalToken: (clientId: string) =>
    request<PortalToken>('/api/portal/tokens', {
      method: 'POST',
      body: JSON.stringify({ clientId })
    }),
  entitlements: () => request<BillingEntitlements>('/api/billing/entitlements'),
  checkout: () =>
    request<BillingSession>('/api/billing/checkout', { method: 'POST', body: JSON.stringify({}) }),
  portal: () =>
    request<BillingSession>('/api/billing/portal', { method: 'POST', body: JSON.stringify({}) })
};
