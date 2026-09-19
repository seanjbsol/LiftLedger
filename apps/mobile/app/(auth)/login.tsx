import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { useAuth } from '../../src/auth';
import { colors, space } from '../../src/theme';
import { Banner, Button, Field, ScrollScreen, Subtitle, Title } from '../../src/ui';

export default function LoginScreen() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('owner@humberhire.demo');
  const [password, setPassword] = useState('DemoPass123!');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
      router.replace('/(app)');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign in failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollScreen navy>
      <Text style={{ color: colors.amber, fontWeight: '800', letterSpacing: 1.4, marginBottom: space.md }}>
        LIFTLEDGER
      </Text>
      <Title light>Sign in</Title>
      <Subtitle light>
        Inspection records for UK workshops, hire fleets and examiners. LOLER / PUWER working records — not an HSE
        certificate.
      </Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      <View style={{ marginTop: space.lg, backgroundColor: colors.card, borderRadius: 16, padding: space.md }}>
        <Field label="Email" autoCapitalize="none" keyboardType="email-address" value={email} onChangeText={setEmail} />
        <Field label="Password" secureTextEntry value={password} onChangeText={setPassword} />
        <Button label={busy ? 'Signing in…' : 'Sign in'} onPress={onSubmit} disabled={busy} />
        <Link href="/(auth)/register" style={{ marginTop: space.md, color: colors.amber, fontWeight: '700' }}>
          Register a workshop or hire company
        </Link>
      </View>
    </ScrollScreen>
  );
}
