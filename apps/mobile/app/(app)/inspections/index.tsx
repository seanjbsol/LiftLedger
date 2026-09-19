import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api } from '../../../src/api';
import type { InspectionSummary } from '../../../src/types';
import { Button, Card, Loading, Pill, ScrollScreen, Subtitle, Title, formatDate } from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function InspectionsScreen() {
  const router = useRouter();
  const [items, setItems] = useState<InspectionSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useFocusEffect(
    useCallback(() => {
      api
        .inspections()
        .then(setItems)
        .catch((err) => setError(err instanceof Error ? err.message : 'Could not load examinations.'))
        .finally(() => setLoading(false));
    }, [])
  );

  return (
    <ScrollScreen>
      <Title>Examinations</Title>
      <Subtitle>Thorough examinations and other inspections for this organisation.</Subtitle>
      <Button label="Start examination" onPress={() => router.push('/(app)/inspections/new')} />
      {error ? <Text style={{ color: colors.danger, marginTop: 12 }}>{error}</Text> : null}
      {loading ? <Loading /> : null}
      {items.map((item) => (
        <Pressable key={item.id} onPress={() => router.push(`/(app)/inspections/${item.id}`)}>
          <Card>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: 12 }}>
              <View style={{ flex: 1 }}>
                <Text style={{ fontWeight: '800', color: colors.ink }}>
                  {item.assetNumber} · {item.assetName}
                </Text>
                <Text style={{ color: colors.muted, marginTop: 4 }}>
                  {formatDate(item.examinationDate)} · {item.examinerName}
                </Text>
                <Text style={{ color: colors.muted, marginTop: 4 }}>
                  {item.certificateNumber ?? 'Draft — not yet issued'}
                </Text>
              </View>
              <Pill label={item.result ?? item.status} tone={item.result ?? item.status} />
            </View>
          </Card>
        </Pressable>
      ))}
    </ScrollScreen>
  );
}
