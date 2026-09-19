import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useCallback, useState } from 'react';
import { Image, Pressable, Text, View } from 'react-native';
import { API_URL, api, ApiError } from '../../../src/api';
import { useAuth } from '../../../src/auth';
import { useEntitlements } from '../../../src/entitlements';
import { pickImage } from '../../../src/photos';
import type { DefectDto, MemberDto } from '../../../src/types';
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
  UpgradeGate
} from '../../../src/ui';
import { colors } from '../../../src/theme';

export default function DefectDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { token } = useAuth();
  const { canUseDefects } = useEntitlements();
  const [defect, setDefect] = useState<DefectDto | null>(null);
  const [members, setMembers] = useState<MemberDto[]>([]);
  const [closeNotes, setCloseNotes] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!id) {
      return;
    }
    try {
      const [item, people] = await Promise.all([api.defect(id), api.members()]);
      setDefect(item);
      setMembers(people);
      setCloseNotes(item.closeNotes ?? '');
      setError(null);
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setError(null);
      } else {
        setError(err instanceof Error ? err.message : 'Defect was not found.');
      }
    }
  }, [id]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const run = async (work: () => Promise<void>, success: string) => {
    setBusy(true);
    setError(null);
    try {
      await work();
      setNotice(success);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not update the defect.');
    } finally {
      setBusy(false);
    }
  };

  const upload = async (kind: 'Before' | 'After') => {
    const picked = await pickImage();
    if (!picked || !id) {
      if (!picked) {
        setError('Choose a JPEG, PNG or WebP photo (web client).');
      }
      return;
    }
    await run(async () => {
      await api.uploadDefectPhoto(id, kind, picked.file, picked.name);
    }, `${kind} photo attached.`);
  };

  if (!canUseDefects) {
    return (
      <ScrollScreen>
        <Title>Defect</Title>
        <UpgradeGate feature="Defect workflow" />
      </ScrollScreen>
    );
  }

  if (!defect) {
    return (
      <ScrollScreen>
        {error ? <Banner tone="danger" text={error} /> : <Loading />}
      </ScrollScreen>
    );
  }

  const open = defect.status === 'Open' || defect.status === 'Assigned' || defect.status === 'RetestRequired';

  return (
    <ScrollScreen>
      <Title>{defect.assetNumber}</Title>
      <Subtitle>
        {defect.assetName} · {defect.severity}
      </Subtitle>
      <View style={{ marginTop: 12 }}>
        <Pill label={defect.status} tone={defect.status} />
      </View>
      {error ? <Banner tone="danger" text={error} /> : null}
      {notice ? <Banner tone="success" text={notice} /> : null}
      <Card>
        <Text style={{ fontWeight: '700' }}>{defect.description}</Text>
        {defect.assignedToName ? (
          <Text style={{ color: colors.muted, marginTop: 8 }}>Assigned to {defect.assignedToName}</Text>
        ) : null}
      </Card>

      <Text style={{ marginTop: 18, fontWeight: '800' }}>Photos</Text>
      <View style={{ flexDirection: 'row', gap: 10, flexWrap: 'wrap' }}>
        {defect.photos.map((photo) => (
          <View key={photo.id} style={{ width: 140 }}>
            <Image
              source={{
                uri: `${API_URL}/api/defects/${defect.id}/photos/${photo.id}`,
                headers: token ? { Authorization: `Bearer ${token}` } : undefined
              }}
              style={{ width: 140, height: 100, borderRadius: 10, marginTop: 8, backgroundColor: colors.line }}
            />
            <Text style={{ color: colors.muted, marginTop: 4 }}>{photo.kind}</Text>
          </View>
        ))}
      </View>
      {open ? (
        <>
          <Button label="Attach before photo" tone="secondary" onPress={() => void upload('Before')} disabled={busy} />
          <Button label="Attach after photo" tone="secondary" onPress={() => void upload('After')} disabled={busy} />
        </>
      ) : null}

      {defect.status === 'Open' ? (
        <>
          <Text style={{ marginTop: 18, fontWeight: '800' }}>Assign</Text>
          {members.map((member) => (
            <Pressable
              key={member.userId}
              onPress={() =>
                void run(
                  async () => {
                    await api.assignDefect(defect.id, member.userId);
                  },
                  `Assigned to ${member.fullName}.`
                )
              }
            >
              <Card>
                <Text style={{ fontWeight: '700' }}>{member.fullName}</Text>
                <Text style={{ color: colors.muted, marginTop: 4 }}>
                  {member.role} · {member.email}
                </Text>
              </Card>
            </Pressable>
          ))}
        </>
      ) : null}

      {open && defect.status !== 'RetestRequired' ? (
        <>
          <Field label="Close notes" multiline value={closeNotes} onChangeText={setCloseNotes} />
          <Button
            label={busy ? 'Saving…' : 'Close defect'}
            onPress={() =>
              void run(async () => {
                await api.closeDefect(defect.id, { closeNotes, requiresRetest: false });
              }, 'Defect closed.')
            }
            disabled={busy}
          />
          <Button
            label="Close — retest required"
            tone="secondary"
            onPress={() =>
              void run(async () => {
                await api.closeDefect(defect.id, { closeNotes, requiresRetest: true });
              }, 'Marked retest required.')
            }
            disabled={busy}
          />
        </>
      ) : null}

      {defect.status === 'RetestRequired' ? (
        <Button
          label="Start retest examination"
          onPress={() =>
            void run(async () => {
              const inspection = await api.startRetest(defect.id);
              router.push(`/(app)/inspections/${inspection.id}`);
            }, 'Retest examination started.')
          }
          disabled={busy}
        />
      ) : null}
    </ScrollScreen>
  );
}
