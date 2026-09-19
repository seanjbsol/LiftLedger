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
};

export type DefectDto = {
  id: string;
  description: string;
  severity: DefectSeverity;
  category?: string | null;
  requiresImmediateWithdrawal: boolean;
  rectifiedAt?: string | null;
  notes?: string | null;
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

export type InspectionDetail = InspectionSummary & {
  tenantId: string;
  examinerUserId: string;
  safeWorkingLoad?: string | null;
  particularsOfExamination?: string | null;
  particularsOfDefects?: string | null;
  repairsRequired?: string | null;
  examinerQualifications?: string | null;
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
