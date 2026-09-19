import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, Text } from 'react-native';
import { api } from '../../../src/api';
import type { AssetSummary } from '../../../src/types';
import { Banner, Card, Loading, ScrollScreen, Subtitle, Title } from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function StartInspectionScreen() {
  const router = useRouter();
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useFocusEffect(
    useCallback(() => {
      api.assets().then(setAssets).catch((err) => setError(err instanceof Error ? err.message : 'Could not load assets.'));
    }, [])
  );

  const start = async (assetId: string) => {
    setBusyId(assetId);
    setError(null);
    try {
      const inspection = await api.startInspection(assetId);
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
      <Subtitle>Choose the asset. The form is saved as a draft, then completed on the next screen.</Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
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
