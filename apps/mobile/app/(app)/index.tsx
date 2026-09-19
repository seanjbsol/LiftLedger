import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useState, type ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api } from '../../src/api';
import { useAuth } from '../../src/auth';
import { listOfflineDrafts, type OfflineDraft } from '../../src/drafts';
import type { DashboardResponse } from '../../src/types';
import { Banner, Card, Loading, Pill, ScrollScreen, Subtitle, Title, formatDate } from '../../src/ui';
import { colors } from '../../src/theme';

export default function HomeScreen() {
  const { tenant, user } = useAuth();
  const router = useRouter();
  const [data, setData] = useState<DashboardResponse | null>(null);
  const [drafts, setDrafts] = useState<OfflineDraft[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useFocusEffect(
    useCallback(() => {
      let cancelled = false;
      (async () => {
        setLoading(true);
        try {
          const [dashboard, offline] = await Promise.all([api.dashboard(), listOfflineDrafts()]);
          if (!cancelled) {
            setData(dashboard);
            setDrafts(offline);
            setError(null);
          }
        } catch (err) {
          if (!cancelled) {
            setError(err instanceof Error ? err.message : 'Could not load dashboard.');
          }
        } finally {
          if (!cancelled) {
            setLoading(false);
          }
        }
      })();
      return () => {
        cancelled = true;
      };
    }, [])
  );

  return (
    <ScrollScreen>
      <Title>Good day{user.fullName ? `, ${user.fullName.split(' ')[0]}` : ''}</Title>
      <Subtitle>
        {tenant.name} — due examinations, open defects and recent thorough examinations.
      </Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      {drafts.length > 0 ? (
        <Banner
          tone="info"
          text={`${drafts.length} examination${drafts.length === 1 ? '' : 's'} saved on this device and waiting to send.`}
        />
      ) : null}
      {loading || !data ? (
        <Loading />
      ) : (
        <>
          <View style={{ flexDirection: 'row', gap: 10, marginTop: 16 }}>
            <Stat label="Overdue" value={data.overdueCount} />
            <Stat label="Due soon" value={data.dueSoonCount} />
            <Stat label="Open defects" value={data.openDefectCount} />
          </View>
          <Section title="Overdue" empty="No overdue examinations.">
            {data.overdue.map((asset) => (
              <Pressable key={asset.id} onPress={() => router.push(`/(app)/assets/${asset.id}`)}>
                <Card>
                  <Row title={asset.assetNumber} subtitle={asset.name} pill={asset.dueStatus} date={asset.nextExaminationDue} />
                </Card>
              </Pressable>
            ))}
          </Section>
          <Section title="Due within 30 days" empty="Nothing due in the next 30 days.">
            {data.dueSoon.map((asset) => (
              <Pressable key={asset.id} onPress={() => router.push(`/(app)/assets/${asset.id}`)}>
                <Card>
                  <Row title={asset.assetNumber} subtitle={asset.name} pill={asset.dueStatus} date={asset.nextExaminationDue} />
                </Card>
              </Pressable>
            ))}
          </Section>
          <Section title="Recent examinations" empty="No examinations recorded yet.">
            {data.recentInspections.map((item) => (
              <Pressable key={item.id} onPress={() => router.push(`/(app)/inspections/${item.id}`)}>
                <Card>
                  <Row
                    title={item.assetNumber}
                    subtitle={`${item.examinerName} · ${formatDate(item.examinationDate)}`}
                    pill={item.result ?? item.status}
                  />
                </Card>
              </Pressable>
            ))}
          </Section>
        </>
      )}
    </ScrollScreen>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <View style={{ flex: 1, backgroundColor: colors.navy, borderRadius: 14, padding: 14 }}>
      <Text style={{ color: colors.amber, fontSize: 22, fontWeight: '800' }}>{value}</Text>
      <Text style={{ color: '#C9D4E0', marginTop: 4, fontSize: 12, fontWeight: '700' }}>{label}</Text>
    </View>
  );
}

function Section({
  title,
  empty,
  children
}: {
  title: string;
  empty: string;
  children: ReactNode;
}) {
  const items = Array.isArray(children) ? children : children ? [children] : [];
  return (
    <View style={{ marginTop: 22 }}>
      <Text style={{ fontSize: 16, fontWeight: '800', color: colors.ink }}>{title}</Text>
      {items.length === 0 ? (
        <Text style={{ color: colors.muted, marginTop: 8 }}>{empty}</Text>
      ) : (
        items
      )}
    </View>
  );
}

function Row({
  title,
  subtitle,
  pill,
  date
}: {
  title: string;
  subtitle: string;
  pill?: string | null;
  date?: string | null;
}) {
  return (
    <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: 12, alignItems: 'center' }}>
      <View style={{ flex: 1 }}>
        <Text style={{ fontWeight: '800', color: colors.ink }}>{title}</Text>
        <Text style={{ color: colors.muted, marginTop: 4 }}>{subtitle}</Text>
        {date ? <Text style={{ color: colors.muted, marginTop: 4 }}>{formatDate(date)}</Text> : null}
      </View>
      {pill ? <Pill label={pill} tone={pill} /> : null}
    </View>
  );
}
