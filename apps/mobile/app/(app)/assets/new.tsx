import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api } from '../../../src/api';
import type { AssetCategory } from '../../../src/types';
import { Banner, Button, Field, ScrollScreen, Subtitle, Title } from '../../../src/ui';
import { colors, space } from '../../../src/theme';

const categories: { value: AssetCategory; label: string }[] = [
  { value: 'Trailer', label: 'Trailer' },
  { value: 'Plant', label: 'Plant' },
  { value: 'LiftingEquipment', label: 'Lifting equipment' },
  { value: 'LiftingAccessory', label: 'Lifting accessory' },
  { value: 'Other', label: 'Other' }
];

export default function NewAssetScreen() {
  const router = useRouter();
  const [assetNumber, setAssetNumber] = useState('');
  const [name, setName] = useState('');
  const [category, setCategory] = useState<AssetCategory>('Trailer');
  const [make, setMake] = useState('');
  const [model, setModel] = useState('');
  const [serialNumber, setSerialNumber] = useState('');
  const [identificationCode, setIdentificationCode] = useState('');
  const [safeWorkingLoad, setSafeWorkingLoad] = useState('');
  const [nextExaminationDue, setNextExaminationDue] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await api.createAsset({
        assetNumber,
        name,
        category,
        make: make || undefined,
        model: model || undefined,
        serialNumber: serialNumber || undefined,
        identificationCode: identificationCode || undefined,
        safeWorkingLoad: safeWorkingLoad || undefined,
        nextExaminationDue: nextExaminationDue || undefined
      });
      router.replace(`/(app)/assets/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save asset.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollScreen>
      <Title>Add asset</Title>
      <Subtitle>Fleet numbers are unique within your organisation.</Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      <Field label="Fleet number" autoCapitalize="characters" value={assetNumber} onChangeText={setAssetNumber} />
      <Field label="Name" value={name} onChangeText={setName} />
      <Text style={{ fontSize: 13, fontWeight: '700', color: colors.muted, marginBottom: 8 }}>Category</Text>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: space.md }}>
        {categories.map((item) => (
          <Pressable
            key={item.value}
            onPress={() => setCategory(item.value)}
            style={{
              paddingHorizontal: 12,
              paddingVertical: 8,
              borderRadius: 999,
              backgroundColor: category === item.value ? colors.navy : colors.card,
              borderWidth: 1,
              borderColor: colors.line
            }}
          >
            <Text style={{ color: category === item.value ? colors.paper : colors.ink, fontWeight: '700' }}>
              {item.label}
            </Text>
          </Pressable>
        ))}
      </View>
      <Field label="Make" value={make} onChangeText={setMake} />
      <Field label="Model" value={model} onChangeText={setModel} />
      <Field label="Serial number" value={serialNumber} onChangeText={setSerialNumber} />
      <Field label="QR / identification code" value={identificationCode} onChangeText={setIdentificationCode} />
      <Field label="Safe working load" value={safeWorkingLoad} onChangeText={setSafeWorkingLoad} />
      <Field
        label="Next examination due (YYYY-MM-DD)"
        placeholder="2026-12-01"
        value={nextExaminationDue}
        onChangeText={setNextExaminationDue}
      />
      <Button label={busy ? 'Saving…' : 'Save asset'} onPress={onSubmit} disabled={busy} />
    </ScrollScreen>
  );
}
