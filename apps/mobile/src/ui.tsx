import { ReactNode } from 'react';
import { useRouter } from 'expo-router';
import {
  ActivityIndicator,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors, space } from './theme';

export function Screen({
  children,
  navy = false
}: {
  children: ReactNode;
  navy?: boolean;
}) {
  return (
    <SafeAreaView style={[styles.safe, navy && { backgroundColor: colors.navy }]}>
      {children}
    </SafeAreaView>
  );
}

export function ScrollScreen({ children, navy = false }: { children: ReactNode; navy?: boolean }) {
  return (
    <Screen navy={navy}>
      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
        {children}
      </ScrollView>
    </Screen>
  );
}

export function Title({ children, light = false }: { children: ReactNode; light?: boolean }) {
  return <Text style={[styles.title, light && { color: colors.paper }]}>{children}</Text>;
}

export function Subtitle({ children, light = false }: { children: ReactNode; light?: boolean }) {
  return <Text style={[styles.subtitle, light && { color: '#C9D4E0' }]}>{children}</Text>;
}

export function Card({ children }: { children: ReactNode }) {
  return <View style={styles.card}>{children}</View>;
}

export function Button({
  label,
  onPress,
  disabled,
  tone = 'primary'
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  tone?: 'primary' | 'secondary' | 'danger';
}) {
  const background =
    tone === 'danger' ? colors.danger : tone === 'secondary' ? colors.navyMid : colors.amber;
  const color = tone === 'primary' ? colors.navy : colors.paper;
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      style={({ pressed }) => [
        styles.button,
        { backgroundColor: background, opacity: disabled ? 0.5 : pressed ? 0.85 : 1 }
      ]}
    >
      <Text style={[styles.buttonLabel, { color }]}>{label}</Text>
    </Pressable>
  );
}

export function Field({
  label,
  ...props
}: TextInputProps & { label: string }) {
  return (
    <View style={{ marginBottom: space.md }}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        placeholderTextColor={colors.muted}
        style={[styles.input, props.multiline && { height: 96, textAlignVertical: 'top' }]}
        {...props}
      />
    </View>
  );
}

export function Banner({ text, tone = 'info' }: { text: string; tone?: 'info' | 'danger' | 'success' }) {
  const background = tone === 'danger' ? '#FEE4E2' : tone === 'success' ? '#DCFAE6' : '#FFF4D6';
  const color = tone === 'danger' ? colors.danger : tone === 'success' ? colors.success : '#7A4D00';
  return (
    <View style={[styles.banner, { backgroundColor: background }]}>
      <Text style={{ color, fontSize: 13, lineHeight: 18 }}>{text}</Text>
    </View>
  );
}

export function Pill({ label, tone }: { label: string; tone?: string }) {
  const map: Record<string, { bg: string; fg: string }> = {
    Overdue: { bg: '#FEE4E2', fg: colors.overdue },
    'Due soon': { bg: '#FEF0C7', fg: colors.dueSoon },
    Scheduled: { bg: '#DCFAE6', fg: colors.success },
    Pass: { bg: '#DCFAE6', fg: colors.success },
    PassWithDefects: { bg: '#FEF0C7', fg: colors.dueSoon },
    Fail: { bg: '#FEE4E2', fg: colors.overdue },
    Assigned: { bg: '#E8EEF4', fg: colors.muted },
    Open: { bg: '#FEF0C7', fg: colors.dueSoon },
    Closed: { bg: '#DCFAE6', fg: colors.success },
    RetestRequired: { bg: '#FEE4E2', fg: colors.overdue },
    Draft: { bg: '#E8EEF4', fg: colors.muted }
  };
  const colorsFor = map[tone ?? label] ?? { bg: '#E8EEF4', fg: colors.muted };
  return (
    <View style={[styles.pill, { backgroundColor: colorsFor.bg }]}>
      <Text style={{ color: colorsFor.fg, fontSize: 12, fontWeight: '700' }}>{label}</Text>
    </View>
  );
}

export function UpgradeGate({
  feature,
  text
}: {
  feature: string;
  text?: string;
}) {
  const router = useRouter();
  return (
    <View>
      <Banner
        tone="info"
        text={text ?? `${feature} is included on LiftLedger Pro. Starter covers the asset list and a simple examination log.`}
      />
      <Button label="View plans in Settings" tone="secondary" onPress={() => router.push('/(app)/settings')} />
    </View>
  );
}

export function Loading() {
  return (
    <View style={{ padding: 40, alignItems: 'center' }}>
      <ActivityIndicator color={colors.navy} />
    </View>
  );
}

export function formatDate(value?: string | null) {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

export function categoryLabel(value: string) {
  return (
    {
      Trailer: 'Trailer',
      Plant: 'Plant',
      LiftingEquipment: 'Lifting equipment',
      LiftingAccessory: 'Lifting accessory',
      Other: 'Other'
    } as Record<string, string>
  )[value] ?? value;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.paper },
  scroll: { padding: space.lg, paddingBottom: 48 },
  title: { fontSize: 28, fontWeight: '800', color: colors.ink, letterSpacing: -0.4 },
  subtitle: { marginTop: 6, fontSize: 15, color: colors.muted, lineHeight: 21 },
  card: {
    backgroundColor: colors.card,
    borderRadius: 14,
    padding: space.md,
    marginTop: space.md,
    borderWidth: 1,
    borderColor: colors.line
  },
  button: {
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: space.sm
  },
  buttonLabel: { fontWeight: '800', fontSize: 15 },
  label: { fontSize: 13, fontWeight: '700', color: colors.muted, marginBottom: 6 },
  input: {
    backgroundColor: colors.card,
    borderWidth: 1,
    borderColor: colors.line,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 16,
    color: colors.ink
  },
  banner: { borderRadius: 10, padding: 12, marginTop: space.md },
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 10, paddingVertical: 4 }
});
