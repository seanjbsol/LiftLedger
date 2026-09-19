import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api } from '../../../src/api';
import type { AssetSummary } from '../../../src/types';
import { Button, Card, Field, Loading, Pill, ScrollScreen, Subtitle, Title, categoryLabel, formatDate } from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function AssetsScreen() {
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (q?: string) => {
    setLoading(true);
    try {
      setAssets(await api.assets(q));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load assets.');
    } finally {
      setLoading(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load(query);
    }, [load, query])
  );

  return (
    <ScrollScreen>
      <Title>Assets</Title>
      <Subtitle>Trailers, plant and lifting equipment for this organisation only.</Subtitle>
      <Field label="Search" value={query} onChangeText={setQuery} placeholder="Fleet number, name or QR code" />
      <Button label="Add asset" onPress={() => router.push('/(app)/assets/new')} />
      {error ? <Text style={{ color: colors.danger, marginTop: 12 }}>{error}</Text> : null}
      {loading ? <Loading /> : null}
      {assets.map((asset) => (
        <Pressable key={asset.id} onPress={() => router.push(`/(app)/assets/${asset.id}`)}>
          <Card>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: 12 }}>
              <View style={{ flex: 1 }}>
                <Text style={{ fontWeight: '800', color: colors.ink }}>{asset.assetNumber}</Text>
                <Text style={{ color: colors.muted, marginTop: 4 }}>
                  {asset.name} · {categoryLabel(asset.category)}
                </Text>
                <Text style={{ color: colors.muted, marginTop: 4 }}>
                  Next due {formatDate(asset.nextExaminationDue)}
                </Text>
              </View>
              <Pill label={asset.dueStatus} tone={asset.dueStatus} />
            </View>
          </Card>
        </Pressable>
      ))}
    </ScrollScreen>
  );
}
