import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Text, View } from 'react-native';
import { api } from '../../../src/api';
import type { AssetDetail } from '../../../src/types';
import {
  Banner,
  Button,
  Card,
  Loading,
  Pill,
  ScrollScreen,
  Subtitle,
  Title,
  categoryLabel,
  formatDate
} from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function AssetDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [asset, setAsset] = useState<AssetDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useFocusEffect(
    useCallback(() => {
      if (!id) {
        return;
      }
      api
        .asset(id)
        .then(setAsset)
        .catch((err) => setError(err instanceof Error ? err.message : 'Asset was not found.'));
    }, [id])
  );

  const start = async () => {
    if (!id) {
      return;
    }
    setBusy(true);
    try {
      const inspection = await api.startInspection(id);
      router.push(`/(app)/inspections/${inspection.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start examination.');
    } finally {
      setBusy(false);
    }
  };

  if (!asset) {
    return (
      <ScrollScreen>
        {error ? <Banner tone="danger" text={error} /> : <Loading />}
      </ScrollScreen>
    );
  }

  return (
    <ScrollScreen>
      <Title>{asset.assetNumber}</Title>
      <Subtitle>
        {asset.name} · {categoryLabel(asset.category)}
      </Subtitle>
      <View style={{ marginTop: 12 }}>
        <Pill label={asset.dueStatus} tone={asset.dueStatus} />
      </View>
      {error ? <Banner tone="danger" text={error} /> : null}
      <Card>
        <Fact label="Make / model" value={[asset.make, asset.model].filter(Boolean).join(' ') || '—'} />
        <Fact label="Serial number" value={asset.serialNumber} />
        <Fact label="QR / ID code" value={asset.identificationCode} />
        <Fact label="Safe working load" value={asset.safeWorkingLoad} />
        <Fact label="Next examination due" value={formatDate(asset.nextExaminationDue)} />
        <Fact label="Client / site" value={[asset.clientName, asset.siteName].filter(Boolean).join(' · ') || 'In-house'} />
      </Card>
      <Button label={busy ? 'Starting…' : 'Start thorough examination'} onPress={start} disabled={busy} />
      <Text style={{ marginTop: 22, fontWeight: '800', color: colors.ink }}>Examination history</Text>
      {asset.recentInspections.length === 0 ? (
        <Text style={{ color: colors.muted, marginTop: 8 }}>No examinations on file yet.</Text>
      ) : (
        asset.recentInspections.map((item) => (
          <Card key={item.id}>
            <Text style={{ fontWeight: '700' }}>{formatDate(item.examinationDate)}</Text>
            <Text style={{ color: colors.muted, marginTop: 4 }}>
              {item.examinerName} · {item.certificateNumber ?? 'Draft'}
            </Text>
            <View style={{ marginTop: 8 }}>
              <Pill label={item.result ?? item.status} tone={item.result ?? item.status} />
            </View>
          </Card>
        ))
      )}
    </ScrollScreen>
  );
}

function Fact({ label, value }: { label: string; value?: string | null }) {
  return (
    <View style={{ marginBottom: 10 }}>
      <Text style={{ color: colors.muted, fontSize: 12, fontWeight: '700' }}>{label}</Text>
      <Text style={{ color: colors.ink, marginTop: 2 }}>{value || '—'}</Text>
    </View>
  );
}
