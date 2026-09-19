import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { api, ApiError } from '../../../src/api';
import { Banner, Button, Field, ScrollScreen, Subtitle, Title, UpgradeGate } from '../../../src/ui';
import { useEntitlements } from '../../../src/entitlements';

export default function RaiseDefectScreen() {
  const { inspectionId } = useLocalSearchParams<{ inspectionId?: string }>();
  const router = useRouter();
  const { canUseDefects } = useEntitlements();
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!inspectionId) {
      setError('Open a completed examination first, then raise a defect from there.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const created = await api.raiseDefect(inspectionId, {
        description,
        category,
        severity: 'Defect'
      });
      router.replace(`/(app)/defects/${created.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setError(err.message);
      } else {
        setError(err instanceof Error ? err.message : 'Could not raise the defect.');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollScreen>
      <Title>Raise defect</Title>
      <Subtitle>Recorded against the completed examination and the same asset.</Subtitle>
      {!canUseDefects ? <UpgradeGate feature="Defect workflow" /> : null}
      {error ? <Banner tone="danger" text={error} /> : null}
      {canUseDefects ? (
        <>
          <Field label="Description" multiline value={description} onChangeText={setDescription} />
          <Field label="Category (optional)" value={category} onChangeText={setCategory} />
          <Button label={busy ? 'Saving…' : 'Raise defect'} onPress={() => void submit()} disabled={busy || !description} />
        </>
      ) : null}
    </ScrollScreen>
  );
}
