import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Image, Text, View } from 'react-native';
import { api, ApiError } from '../../../src/api';
import { useAuth } from '../../../src/auth';
import { useEntitlements } from '../../../src/entitlements';
import type { AssetDetail, ExaminationType } from '../../../src/types';
import {
  Banner,
  Button,
  Card,
  Loading,
  Pill,
  ScrollScreen,
  Subtitle,
  Title,
  UpgradeGate,
  categoryLabel,
  formatDate
} from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function AssetDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { token } = useAuth();
  const { canUseCertificates } = useEntitlements();
  const [asset, setAsset] = useState<AssetDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

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

  const start = async (examinationType: ExaminationType) => {
    if (!id) {
      return;
    }
    setBusy(examinationType);
    try {
      const inspection = await api.startInspection(id, examinationType);
      router.push(`/(app)/inspections/${inspection.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start examination.');
    } finally {
      setBusy(null);
    }
  };

  const openLastCert = async () => {
    const last = asset?.recentInspections.find((item) => item.storedCertificateId || item.status === 'Completed');
    if (!last) {
      setError('No completed working record on file yet.');
      return;
    }
    try {
      if (last.storedCertificateId) {
        await api.openStoredCertificate(last.storedCertificateId);
      } else {
        await api.openInspectionCertificate(last.id);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setError(err.message);
      } else {
        setError(err instanceof Error ? err.message : 'Could not open the last working record.');
      }
    }
  };

  if (!asset) {
    return (
      <ScrollScreen>
        {error ? <Banner tone="danger" text={error} /> : <Loading />}
      </ScrollScreen>
    );
  }

  const last = asset.recentInspections.find((item) => item.status === 'Completed');

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
        <Fact label="Deep link" value={`liftledger://a/${asset.identificationCode ?? asset.assetNumber}`} />
        <Fact label="Safe working load" value={asset.safeWorkingLoad} />
        <Fact label="Next examination due" value={formatDate(asset.nextExaminationDue)} />
        <Fact label="Client / site" value={[asset.clientName, asset.siteName].filter(Boolean).join(' · ') || 'In-house'} />
        {id && token ? (
          <Image
            source={{
              uri: api.qrUrl(id),
              headers: { Authorization: `Bearer ${token}` }
            }}
            style={{ width: 160, height: 160, marginTop: 12, alignSelf: 'flex-start' }}
          />
        ) : null}
        <Text style={{ color: colors.muted, fontSize: 12, marginTop: 8 }}>
          Scan the QR code in the mobile app to open the last working record or start an examination.
        </Text>
      </Card>
      <Button
        label={busy === 'ThoroughExamination' ? 'Starting…' : 'Start LOLER thorough examination'}
        onPress={() => void start('ThoroughExamination')}
        disabled={busy !== null}
      />
      {canUseCertificates ? (
        <Button
          label={busy === 'PuwerInspection' ? 'Starting…' : 'Start PUWER assessment'}
          tone="secondary"
          onPress={() => void start('PuwerInspection')}
          disabled={busy !== null}
        />
      ) : (
        <UpgradeGate feature="PUWER assessment builder" />
      )}
      {last ? (
        <Button label="Open last working record" tone="secondary" onPress={() => void openLastCert()} />
      ) : null}
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
