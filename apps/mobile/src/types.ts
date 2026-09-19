export type Role = 'Owner' | 'Admin' | 'Examiner' | 'Viewer';

export type AssetCategory =
  | 'Trailer'
  | 'Plant'
  | 'LiftingEquipment'
  | 'LiftingAccessory'
  | 'Other';

export type InspectionResult = 'Pass' | 'PassWithDefects' | 'Fail';
export type InspectionStatus = 'Draft' | 'Completed';
export type ExaminationType =
  | 'ThoroughExamination'
  | 'InterimInspection'
  | 'PreUseCheck'
  | 'PuwerInspection';
export type DefectSeverity = 'Observation' | 'Defect' | 'ImmediateDanger';
export type DefectStatus = 'Open' | 'Assigned' | 'Closed' | 'RetestRequired';
export type DefectPhotoKind = 'Before' | 'After';
export type PuwerItemResult = 'Suitable' | 'ActionRequired' | 'NotApplicable';
export type CertificateKind = 'LolerThoroughExamination' | 'PuwerAssessment';

export type TenantSummary = {
  id: string;
  name: string;
  tradingName?: string | null;
  town?: string | null;
  postcode?: string | null;
};

export type UserSummary = {
  id: string;
  email: string;
  fullName: string;
};

export type AuthResponse = {
  token: string;
  user: UserSummary;
  tenant: TenantSummary;
  role: Role;
};

export type AssetSummary = {
  id: string;
  assetNumber: string;
  name: string;
  category: AssetCategory;
  identificationCode?: string | null;
  safeWorkingLoad?: string | null;
  nextExaminationDue?: string | null;
  isArchived: boolean;
  clientName?: string | null;
  siteName?: string | null;
  dueStatus: string;
};

export type InspectionSummary = {
  id: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  examinationType: ExaminationType;
  status: InspectionStatus;
  result?: InspectionResult | null;
  examinationDate: string;
  nextDueDate?: string | null;
  certificateNumber?: string | null;
  examinerName: string;
  storedCertificateId?: string | null;
};

export type DefectPhotoDto = {
  id: string;
  kind: DefectPhotoKind;
  fileName: string;
  contentType: string;
  uploadedAt: string;
};

export type DefectDto = {
  id: string;
  inspectionId: string;
  assetId: string;
  assetNumber: string;
  assetName: string;
  description: string;
  severity: DefectSeverity;
  status: DefectStatus;
  category?: string | null;
  requiresImmediateWithdrawal: boolean;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  assignedAt?: string | null;
  rectifiedAt?: string | null;
  closedAt?: string | null;
  closeNotes?: string | null;
  requiresRetest: boolean;
  retestInspectionId?: string | null;
  notes?: string | null;
  createdAt: string;
  photos: DefectPhotoDto[];
};

export type AssetDetail = AssetSummary & {
  tenantId: string;
  make?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  firstUseDate?: string | null;
  notes?: string | null;
  clientId?: string | null;
  siteId?: string | null;
  recentInspections: InspectionSummary[];
};

export type PuwerAnswerDto = {
  itemId: string;
  result: PuwerItemResult;
  notes?: string | null;
};

export type InspectionDetail = InspectionSummary & {
  tenantId: string;
  examinerUserId: string;
  dateOfLastExamination?: string | null;
  dateOfReport?: string | null;
  safeWorkingLoad?: string | null;
  particularsOfExamination?: string | null;
  particularsOfTests?: string | null;
  particularsOfDefects?: string | null;
  repairsRequired?: string | null;
  examinerQualifications?: string | null;
  employerName?: string | null;
  employerAddress?: string | null;
  premisesAddress?: string | null;
  competentPersonAddress?: string | null;
  authenticatingPerson?: string | null;
  declaration?: string | null;
  puwerTemplateCode?: string | null;
  puwerAnswers: PuwerAnswerDto[];
  storedCertificateId?: string | null;
  createdAt: string;
  completedAt?: string | null;
  defects: DefectDto[];
};

export type DashboardResponse = {
  tenant: TenantSummary;
  assetCount: number;
  overdueCount: number;
  dueSoonCount: number;
  openDefectCount: number;
  overdue: AssetSummary[];
  dueSoon: AssetSummary[];
  recentInspections: InspectionSummary[];
};

export type DefectInput = {
  description: string;
  severity: DefectSeverity;
  category?: string;
  requiresImmediateWithdrawal: boolean;
  notes?: string;
};

export type BillingEntitlements = {
  productCode: string;
  tenantId: string;
  status: string;
  planCode?: string | null;
  planName?: string | null;
  currentPeriodEnd?: string | null;
  hasAccess: boolean;
  planTier: string;
  isPro: boolean;
  canUseCertificates: boolean;
  canUseDefects: boolean;
  canUseClientPortal: boolean;
};

export type BillingSession = {
  url: string;
};

export type PuwerTemplateItem = { id: string; section: string; prompt: string };
export type PuwerTemplate = {
  code: string;
  category: AssetCategory;
  title: string;
  introduction: string;
  items: PuwerTemplateItem[];
};

export type AssetScan = {
  id: string;
  assetNumber: string;
  name: string;
  category: AssetCategory;
  identificationCode?: string | null;
  deepLink: string;
  lastInspectionId?: string | null;
  lastCertificateId?: string | null;
  lastCertificateNumber?: string | null;
  lastExaminationDate?: string | null;
};

export type MemberDto = {
  userId: string;
  fullName: string;
  email: string;
  role: Role;
};

export type ClientDto = {
  id: string;
  name: string;
  contactName?: string | null;
  email?: string | null;
};

export type PortalToken = {
  id: string;
  clientId: string;
  clientName: string;
  token: string;
  publicUrl: string;
  expiresAt: string;
};

export type CertificateSummary = {
  id: string;
  inspectionId: string;
  assetId: string;
  kind: CertificateKind;
  certificateNumber: string;
  templateCode: string;
  assetNumber: string;
  assetName: string;
  issuedAt: string;
};

export type CompleteInspectionPayload = {
  result: InspectionResult;
  nextDueDate: string;
  examinationDate?: string;
  dateOfLastExamination?: string;
  dateOfReport?: string;
  safeWorkingLoad?: string;
  particularsOfExamination?: string;
  particularsOfTests?: string;
  particularsOfDefects?: string;
  repairsRequired?: string;
  examinerQualifications?: string;
  employerName?: string;
  employerAddress?: string;
  premisesAddress?: string;
  competentPersonAddress?: string;
  authenticatingPerson?: string;
  declaration?: string;
  puwerTemplateCode?: string;
  puwerAnswers?: PuwerAnswerDto[];
  defects: DefectInput[];
};
