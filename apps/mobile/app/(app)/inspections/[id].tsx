import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useMemo, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api, ApiError } from '../../../src/api';
import { saveOfflineDraft } from '../../../src/drafts';
import { useEntitlements } from '../../../src/entitlements';
import type {
  DefectInput,
  DefectSeverity,
  InspectionDetail,
  InspectionResult,
  PuwerItemResult,
  PuwerTemplate
} from '../../../src/types';
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
  UpgradeGate,
  formatDate
} from '../../../src/ui';
import { colors, space } from '../../../src/theme';

const results: { value: InspectionResult; label: string }[] = [
  { value: 'Pass', label: 'Pass' },
  { value: 'PassWithDefects', label: 'Pass with defects' },
  { value: 'Fail', label: 'Fail' }
];

const severities: DefectSeverity[] = ['Observation', 'Defect', 'ImmediateDanger'];
const puwerResults: { value: PuwerItemResult; label: string }[] = [
  { value: 'Suitable', label: 'Suitable' },
  { value: 'ActionRequired', label: 'Action required' },
  { value: 'NotApplicable', label: 'N/A' }
];

export default function InspectionDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { canUseCertificates, canUseDefects } = useEntitlements();
  const [inspection, setInspection] = useState<InspectionDetail | null>(null);
  const [template, setTemplate] = useState<PuwerTemplate | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [paywall, setPaywall] = useState<string | null>(null);

  const [result, setResult] = useState<InspectionResult>('Pass');
  const [nextDueDate, setNextDueDate] = useState('');
  const [safeWorkingLoad, setSafeWorkingLoad] = useState('');
  const [particulars, setParticulars] = useState('');
  const [particularsOfTests, setParticularsOfTests] = useState('');
  const [particularsOfDefects, setParticularsOfDefects] = useState('');
  const [repairsRequired, setRepairsRequired] = useState('');
  const [qualifications, setQualifications] = useState('');
  const [employerName, setEmployerName] = useState('');
  const [employerAddress, setEmployerAddress] = useState('');
  const [premisesAddress, setPremisesAddress] = useState('');
  const [competentPersonAddress, setCompetentPersonAddress] = useState('');
  const [declaration, setDeclaration] = useState('');
  const [defects, setDefects] = useState<DefectInput[]>([]);
  const [puwerNotes, setPuwerNotes] = useState<Record<string, { result: PuwerItemResult; notes: string }>>({});

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
        .then(async (item) => {
          setInspection(item);
          setResult(item.result ?? 'Pass');
          setNextDueDate(item.nextDueDate ?? todayPlusSixMonths);
          setSafeWorkingLoad(item.safeWorkingLoad ?? '');
          setParticulars(item.particularsOfExamination ?? '');
          setParticularsOfTests(item.particularsOfTests ?? '');
          setParticularsOfDefects(item.particularsOfDefects ?? '');
          setRepairsRequired(item.repairsRequired ?? '');
          setQualifications(item.examinerQualifications ?? '');
          setEmployerName(item.employerName ?? '');
          setEmployerAddress(item.employerAddress ?? '');
          setPremisesAddress(item.premisesAddress ?? '');
          setCompetentPersonAddress(item.competentPersonAddress ?? '');
          setDeclaration(item.declaration ?? '');
          setDefects(
            item.defects.map((defect) => ({
              description: defect.description,
              severity: defect.severity,
              category: defect.category ?? undefined,
              requiresImmediateWithdrawal: defect.requiresImmediateWithdrawal,
              notes: defect.notes ?? undefined
            }))
          );
          if (item.examinationType === 'PuwerInspection') {
            try {
              const loaded = await api.puwerTemplate(guessCategory(item.puwerTemplateCode));
              setTemplate(loaded);
              const answers: Record<string, { result: PuwerItemResult; notes: string }> = {};
              for (const row of loaded.items) {
                const existing = item.puwerAnswers.find((a) => a.itemId === row.id);
                answers[row.id] = {
                  result: existing?.result ?? 'Suitable',
                  notes: existing?.notes ?? ''
                };
              }
              setPuwerNotes(answers);
            } catch (err) {
              if (err instanceof ApiError && err.status === 402) {
                setPaywall('PUWER assessment templates');
              } else {
                setError(err instanceof Error ? err.message : 'Could not load the PUWER template.');
              }
            }
          }
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
      safeWorkingLoad,
      particularsOfExamination: particulars,
      particularsOfTests,
      particularsOfDefects,
      repairsRequired,
      examinerQualifications: qualifications,
      employerName,
      employerAddress,
      premisesAddress,
      competentPersonAddress,
      declaration,
      puwerTemplateCode: inspection.puwerTemplateCode ?? template?.code,
      puwerAnswers: Object.entries(puwerNotes).map(([itemId, value]) => ({
        itemId,
        result: value.result,
        notes: value.notes
      })),
      defects
    };
    try {
      const updated = await api.completeInspection(id, payload);
      setInspection(updated);
      setNotice(
        updated.storedCertificateId
          ? 'Examination completed. A stored working record is now on file.'
          : 'Examination logged. Upgrade to Pro to issue a stored LOLER/PUWER working record.'
      );
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

  const openRecord = async () => {
    if (!inspection) {
      return;
    }
    try {
      if (inspection.storedCertificateId) {
        await api.openStoredCertificate(inspection.storedCertificateId);
      } else {
        await api.openInspectionCertificate(inspection.id);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setPaywall('Stored certificates');
      } else {
        setError(err instanceof Error ? err.message : 'Could not open the working record.');
      }
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
  const isPuwer = inspection.examinationType === 'PuwerInspection';

  return (
    <ScrollScreen>
      <Title>
        {inspection.assetNumber} · {inspection.assetName}
      </Title>
      <Subtitle>
        {isPuwer ? 'PUWER assessment' : 'LOLER thorough examination'} · {formatDate(inspection.examinationDate)} ·{' '}
        {inspection.examinerName}
      </Subtitle>
      <View style={{ marginTop: 12 }}>
        <Pill label={inspection.result ?? inspection.status} tone={inspection.result ?? inspection.status} />
      </View>
      {error ? <Banner tone="danger" text={error} /> : null}
      {notice ? <Banner tone="success" text={notice} /> : null}
      {paywall ? <UpgradeGate feature={paywall} /> : null}

      {completed ? (
        <>
          <Card>
            <Text style={{ fontWeight: '800' }}>{inspection.certificateNumber}</Text>
            <Text style={{ color: colors.muted, marginTop: 8 }}>{inspection.particularsOfExamination}</Text>
            {inspection.defects.map((defect) => (
              <Pressable
                key={defect.id}
                onPress={() => router.push(`/(app)/defects/${defect.id}`)}
                style={{ marginTop: 8 }}
              >
                <Text>
                  · {defect.description} ({defect.status})
                </Text>
              </Pressable>
            ))}
          </Card>
          {canUseCertificates ? (
            <Button label="Open stored working record" tone="secondary" onPress={() => void openRecord()} />
          ) : (
            <UpgradeGate feature="Certificate builders" />
          )}
          {canUseDefects ? (
            <Button
              label="Raise a defect"
              tone="secondary"
              onPress={() => router.push(`/(app)/defects/new?inspectionId=${inspection.id}`)}
            />
          ) : inspection.defects.length > 0 ? (
            <UpgradeGate feature="Defect workflow" />
          ) : null}
          <Banner text="This is a LiftLedger working record in the style of LOLER Schedule 1 / PUWER assessment notes. It is not an HSE-certified document." />
        </>
      ) : (
        <>
          {isPuwer && !canUseCertificates ? (
            <UpgradeGate feature="PUWER assessment builder" />
          ) : (
            <>
              <Text style={{ marginTop: space.lg, fontWeight: '800' }}>Result</Text>
              <ChoiceRow
                options={results}
                value={result}
                onChange={setResult}
              />
              <Field label="Next due / review by (YYYY-MM-DD)" value={nextDueDate} onChangeText={setNextDueDate} />
              <Field label="Safe working load" value={safeWorkingLoad} onChangeText={setSafeWorkingLoad} />
              {!isPuwer ? (
                <>
                  <Field label="Employer for whom the examination was made" value={employerName} onChangeText={setEmployerName} />
                  <Field label="Employer address" value={employerAddress} onChangeText={setEmployerAddress} />
                  <Field label="Premises at which the examination was made" value={premisesAddress} onChangeText={setPremisesAddress} />
                  <Field
                    label="Competent person address"
                    value={competentPersonAddress}
                    onChangeText={setCompetentPersonAddress}
                  />
                  <Field
                    label="Particulars of the examination"
                    multiline
                    value={particulars}
                    onChangeText={setParticulars}
                  />
                  <Field
                    label="Particulars of any test carried out"
                    multiline
                    value={particularsOfTests}
                    onChangeText={setParticularsOfTests}
                  />
                </>
              ) : (
                <>
                  <Banner text={template?.introduction ?? 'PUWER workplace assessment notes. Not an HSE-certified form.'} />
                  {template?.items.map((item) => (
                    <Card key={item.id}>
                      <Text style={{ fontWeight: '800' }}>{item.section}</Text>
                      <Text style={{ color: colors.muted, marginTop: 6 }}>{item.prompt}</Text>
                      <ChoiceRow
                        options={puwerResults}
                        value={puwerNotes[item.id]?.result ?? 'Suitable'}
                        onChange={(value) =>
                          setPuwerNotes((current) => ({
                            ...current,
                            [item.id]: { result: value, notes: current[item.id]?.notes ?? '' }
                          }))
                        }
                      />
                      <Field
                        label="Notes"
                        value={puwerNotes[item.id]?.notes ?? ''}
                        onChangeText={(text) =>
                          setPuwerNotes((current) => ({
                            ...current,
                            [item.id]: { result: current[item.id]?.result ?? 'Suitable', notes: text }
                          }))
                        }
                      />
                    </Card>
                  ))}
                  <Field label="Summary of the assessment" multiline value={particulars} onChangeText={setParticulars} />
                </>
              )}
              <Field
                label="Particulars of any defect"
                multiline
                value={particularsOfDefects}
                onChangeText={setParticularsOfDefects}
              />
              <Field label="Repair / further examination required" multiline value={repairsRequired} onChangeText={setRepairsRequired} />
              <Field label="Declaration" multiline value={declaration} onChangeText={setDeclaration} />
              <Field label="Examiner qualifications" value={qualifications} onChangeText={setQualifications} />
              <Button
                label="Add defect"
                tone="secondary"
                onPress={() =>
                  setDefects((current) => [
                    ...current,
                    { description: '', severity: 'Defect', requiresImmediateWithdrawal: false }
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
                  <ChoiceRow
                    options={severities.map((value) => ({ value, label: value }))}
                    value={defect.severity}
                    onChange={(value) =>
                      setDefects((current) => current.map((item, i) => (i === index ? { ...item, severity: value } : item)))
                    }
                  />
                </Card>
              ))}
              <Button
                label={busy ? 'Saving…' : isPuwer ? 'Complete PUWER assessment' : 'Complete thorough examination'}
                onPress={() => void complete()}
                disabled={busy}
              />
            </>
          )}
        </>
      )}
    </ScrollScreen>
  );
}

function ChoiceRow<T extends string>({
  options,
  value,
  onChange
}: {
  options: { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginVertical: 10 }}>
      {options.map((item) => (
        <Pressable
          key={item.value}
          onPress={() => onChange(item.value)}
          style={{
            paddingHorizontal: 12,
            paddingVertical: 8,
            borderRadius: 999,
            backgroundColor: value === item.value ? colors.navy : colors.card,
            borderWidth: 1,
            borderColor: colors.line
          }}
        >
          <Text style={{ color: value === item.value ? colors.paper : colors.ink, fontWeight: '700' }}>{item.label}</Text>
        </Pressable>
      ))}
    </View>
  );
}

function guessCategory(templateCode?: string | null) {
  if (templateCode?.includes('trailer')) {
    return 'Trailer';
  }
  if (templateCode?.includes('accessory')) {
    return 'LiftingAccessory';
  }
  if (templateCode?.includes('lifting')) {
    return 'LiftingEquipment';
  }
  if (templateCode?.includes('other')) {
    return 'Other';
  }
  return 'Plant';
}
