import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, Text } from 'react-native';
import { api } from '../../../src/api';
import { useEntitlements } from '../../../src/entitlements';
import type { AssetSummary, ExaminationType } from '../../../src/types';
import { Banner, Card, Loading, ScrollScreen, Subtitle, Title, UpgradeGate } from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function StartInspectionScreen() {
  const router = useRouter();
  const { canUseCertificates } = useEntitlements();
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [type, setType] = useState<ExaminationType>('ThoroughExamination');

  useFocusEffect(
    useCallback(() => {
      api.assets().then(setAssets).catch((err) => setError(err instanceof Error ? err.message : 'Could not load assets.'));
    }, [])
  );

  const start = async (assetId: string) => {
    if (type === 'PuwerInspection' && !canUseCertificates) {
      return;
    }
    setBusyId(assetId);
    setError(null);
    try {
      const inspection = await api.startInspection(assetId, type);
      router.replace(`/(app)/inspections/${inspection.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start examination.');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <ScrollScreen>
      <Title>Start examination</Title>
      <Subtitle>Choose LOLER thorough examination or a PUWER assessment, then the asset.</Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      <Pressable onPress={() => setType('ThoroughExamination')}>
        <Card>
          <Text style={{ fontWeight: '800', color: type === 'ThoroughExamination' ? colors.ink : colors.muted }}>
            LOLER thorough examination
          </Text>
        </Card>
      </Pressable>
      <Pressable onPress={() => setType('PuwerInspection')}>
        <Card>
          <Text style={{ fontWeight: '800', color: type === 'PuwerInspection' ? colors.ink : colors.muted }}>
            PUWER assessment
          </Text>
        </Card>
      </Pressable>
      {type === 'PuwerInspection' && !canUseCertificates ? <UpgradeGate feature="PUWER assessment builder" /> : null}
      {assets.length === 0 ? <Loading /> : null}
      {assets.map((asset) => (
        <Pressable key={asset.id} onPress={() => start(asset.id)} disabled={busyId !== null}>
          <Card>
            <Text style={{ fontWeight: '800', color: colors.ink }}>{asset.assetNumber}</Text>
            <Text style={{ color: colors.muted, marginTop: 4 }}>
              {asset.name} {busyId === asset.id ? '· starting…' : ''}
            </Text>
          </Card>
        </Pressable>
      ))}
    </ScrollScreen>
  );
}
