import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { Linking, Pressable, Text, View } from 'react-native';
import { api } from '../../../src/api';
import { saveOfflineDraft } from '../../../src/drafts';
import type { DefectInput, DefectSeverity, InspectionDetail, InspectionResult } from '../../../src/types';
import {
  Banner,
  Button,
  Card,
  Field,
  Loading,
  Pill,
  ScrollScreen,
  Subtitle,
  Title,
  formatDate
} from '../../../src/ui';
import { colors, space } from '../../../src/theme';

const results: { value: InspectionResult; label: string }[] = [
  { value: 'Pass', label: 'Pass' },
  { value: 'PassWithDefects', label: 'Pass with defects' },
  { value: 'Fail', label: 'Fail' }
];

export default function InspectionDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [inspection, setInspection] = useState<InspectionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [result, setResult] = useState<InspectionResult>('Pass');
  const [nextDueDate, setNextDueDate] = useState('');
  const [particulars, setParticulars] = useState('');
  const [particularsOfDefects, setParticularsOfDefects] = useState('');
  const [repairsRequired, setRepairsRequired] = useState('');
  const [qualifications, setQualifications] = useState('');
  const [defects, setDefects] = useState<DefectInput[]>([]);

  const todayPlusSixMonths = useMemo(() => {
    const date = new Date();
    date.setMonth(date.getMonth() + 6);
    return date.toISOString().slice(0, 10);
  }, []);

  useFocusEffect(
    useCallback(() => {
      if (!id) {
        return;
      }
      api
        .inspection(id)
        .then((item) => {
          setInspection(item);
          setResult(item.result ?? 'Pass');
          setNextDueDate(item.nextDueDate ?? todayPlusSixMonths);
          setParticulars(item.particularsOfExamination ?? '');
          setParticularsOfDefects(item.particularsOfDefects ?? '');
          setRepairsRequired(item.repairsRequired ?? '');
          setQualifications(item.examinerQualifications ?? '');
          setDefects(
            item.defects.map((defect) => ({
              description: defect.description,
              severity: defect.severity,
              category: defect.category ?? undefined,
              requiresImmediateWithdrawal: defect.requiresImmediateWithdrawal,
              notes: defect.notes ?? undefined
            }))
          );
        })
        .catch((err) => setError(err instanceof Error ? err.message : 'Examination was not found.'));
    }, [id, todayPlusSixMonths])
  );

  const complete = async () => {
    if (!id || !inspection) {
      return;
    }
    setBusy(true);
    setError(null);
    setNotice(null);
    const payload = {
      result,
      nextDueDate,
      particularsOfExamination: particulars,
      particularsOfDefects,
      repairsRequired,
      examinerQualifications: qualifications,
      defects
    };
    try {
      const updated = await api.completeInspection(id, payload);
      setInspection(updated);
      setNotice('Examination completed. A working record of thorough examination is now available.');
    } catch (err) {
      await saveOfflineDraft({
        inspectionId: id,
        assetNumber: inspection.assetNumber,
        savedAt: new Date().toISOString(),
        payload
      });
      setError(
        `${err instanceof Error ? err.message : 'Could not submit.'} A copy has been saved on this device.`
      );
    } finally {
      setBusy(false);
    }
  };

  if (!inspection) {
    return (
      <ScrollScreen>
        {error ? <Banner tone="danger" text={error} /> : <Loading />}
      </ScrollScreen>
    );
  }

  const completed = inspection.status === 'Completed';

  return (
    <ScrollScreen>
      <Title>
        {inspection.assetNumber} · {inspection.assetName}
      </Title>
      <Subtitle>
        {formatDate(inspection.examinationDate)} · {inspection.examinerName}
      </Subtitle>
      <View style={{ marginTop: 12 }}>
        <Pill label={inspection.result ?? inspection.status} tone={inspection.result ?? inspection.status} />
      </View>
      {error ? <Banner tone="danger" text={error} /> : null}
      {notice ? <Banner tone="success" text={notice} /> : null}

      {completed ? (
        <>
          <Card>
            <Text style={{ fontWeight: '800' }}>{inspection.certificateNumber}</Text>
            <Text style={{ color: colors.muted, marginTop: 8 }}>{inspection.particularsOfExamination}</Text>
            {inspection.defects.map((defect) => (
              <Text key={defect.id} style={{ marginTop: 8 }}>
                · {defect.description}
              </Text>
            ))}
          </Card>
          <Button
            label="Open HTML record"
            tone="secondary"
            onPress={() => Linking.openURL(api.certificateUrl(inspection.id))}
          />
          <Banner
            text="This is a LiftLedger working record in the style of LOLER Schedule 1. It is not an HSE-certified document."
          />
        </>
      ) : (
        <>
          <Text style={{ marginTop: space.lg, fontWeight: '800' }}>Result</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginVertical: 10 }}>
            {results.map((item) => (
              <Pressable
                key={item.value}
                onPress={() => setResult(item.value)}
                style={{
                  paddingHorizontal: 12,
                  paddingVertical: 8,
                  borderRadius: 999,
                  backgroundColor: result === item.value ? colors.navy : colors.card,
                  borderWidth: 1,
                  borderColor: colors.line
                }}
              >
                <Text style={{ color: result === item.value ? colors.paper : colors.ink, fontWeight: '700' }}>
                  {item.label}
                </Text>
              </Pressable>
            ))}
          </View>
          <Field label="Next due (YYYY-MM-DD)" value={nextDueDate} onChangeText={setNextDueDate} />
          <Field
            label="Particulars of the examination"
            multiline
            value={particulars}
            onChangeText={setParticulars}
          />
          <Field
            label="Particulars of any defect"
            multiline
            value={particularsOfDefects}
            onChangeText={setParticularsOfDefects}
          />
          <Field label="Repairs / further examination required" multiline value={repairsRequired} onChangeText={setRepairsRequired} />
          <Field label="Examiner qualifications" value={qualifications} onChangeText={setQualifications} />
          <Button
            label="Add defect"
            tone="secondary"
            onPress={() =>
              setDefects((current) => [
                ...current,
                { description: '', severity: 'Defect' as DefectSeverity, requiresImmediateWithdrawal: false }
              ])
            }
          />
          {defects.map((defect, index) => (
            <Card key={`${index}`}>
              <Field
                label={`Defect ${index + 1}`}
                value={defect.description}
                onChangeText={(text) =>
                  setDefects((current) => current.map((item, i) => (i === index ? { ...item, description: text } : item)))
                }
              />
            </Card>
          ))}
          <Button label={busy ? 'Saving…' : 'Complete examination'} onPress={complete} disabled={busy} />
        </>
      )}
    </ScrollScreen>
  );
}
