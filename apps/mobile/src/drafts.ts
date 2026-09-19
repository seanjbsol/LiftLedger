import AsyncStorage from '@react-native-async-storage/async-storage';
import type { DefectInput, InspectionResult } from './types';

const KEY = 'liftledger.offline-drafts';

export type OfflineDraft = {
  inspectionId: string;
  assetNumber: string;
  savedAt: string;
  payload: {
    result: InspectionResult;
    nextDueDate: string;
    particularsOfExamination?: string;
    particularsOfDefects?: string;
    repairsRequired?: string;
    examinerQualifications?: string;
    defects: DefectInput[];
  };
};

async function readAll(): Promise<OfflineDraft[]> {
  const raw = await AsyncStorage.getItem(KEY);
  return raw ? (JSON.parse(raw) as OfflineDraft[]) : [];
}

export async function saveOfflineDraft(draft: OfflineDraft) {
  const all = await readAll();
  const next = [draft, ...all.filter((item) => item.inspectionId !== draft.inspectionId)];
  await AsyncStorage.setItem(KEY, JSON.stringify(next));
}

export async function listOfflineDrafts() {
  return readAll();
}

export async function removeOfflineDraft(inspectionId: string) {
  const all = await readAll();
  await AsyncStorage.setItem(KEY, JSON.stringify(all.filter((item) => item.inspectionId !== inspectionId)));
}
