import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api, ApiError } from '../../../src/api';
import { useEntitlements } from '../../../src/entitlements';
import type { DefectDto } from '../../../src/types';
import { Banner, Card, Loading, Pill, ScrollScreen, Subtitle, Title, UpgradeGate } from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function DefectsScreen() {
  const router = useRouter();
  const { canUseDefects } = useEntitlements();
  const [items, setItems] = useState<DefectDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useFocusEffect(
    useCallback(() => {
      if (!canUseDefects) {
        setLoading(false);
        return;
      }
      api
        .defects()
        .then(setItems)
        .catch((err) => {
          if (err instanceof ApiError && err.status === 402) {
            setError(null);
          } else {
            setError(err instanceof Error ? err.message : 'Could not load defects.');
          }
        })
        .finally(() => setLoading(false));
    }, [canUseDefects])
  );

  return (
    <ScrollScreen>
      <Title>Defects</Title>
      <Subtitle>Raise from an examination, assign, photograph before and after, then close or mark a retest.</Subtitle>
      {!canUseDefects ? <UpgradeGate feature="Defect workflow" /> : null}
      {error ? <Banner tone="danger" text={error} /> : null}
      {canUseDefects && loading ? <Loading /> : null}
      {canUseDefects
        ? items.map((item) => (
            <Pressable key={item.id} onPress={() => router.push(`/(app)/defects/${item.id}`)}>
              <Card>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: 12 }}>
                  <View style={{ flex: 1 }}>
                    <Text style={{ fontWeight: '800', color: colors.ink }}>
                      {item.assetNumber} · {item.assetName}
                    </Text>
                    <Text style={{ color: colors.muted, marginTop: 4 }}>{item.description}</Text>
                  </View>
                  <Pill label={item.status} tone={item.status} />
                </View>
              </Card>
            </Pressable>
          ))
        : null}
      {canUseDefects && !loading && items.length === 0 ? (
        <Text style={{ color: colors.muted, marginTop: 12 }}>No defects recorded yet. Raise one from a completed examination.</Text>
      ) : null}
    </ScrollScreen>
  );
}
