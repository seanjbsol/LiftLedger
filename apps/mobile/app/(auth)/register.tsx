import { Link, useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';
import { useAuth } from '../../src/auth';
import { colors, space } from '../../src/theme';
import { Banner, Button, Field, ScrollScreen, Subtitle, Title } from '../../src/ui';

export default function RegisterScreen() {
  const { register } = useAuth();
  const router = useRouter();
  const [organisationName, setOrganisationName] = useState('');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [town, setTown] = useState('');
  const [postcode, setPostcode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await register({
        organisationName,
        fullName,
        email: email.trim(),
        password,
        town: town || undefined,
        postcode: postcode || undefined
      });
      router.replace('/(app)');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <ScrollScreen navy>
      <Title light>Create organisation</Title>
      <Subtitle light>
        Registration creates your tenant (workshop or hire company) and an Owner account. Every record is scoped to
        that organisation.
      </Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      <View style={{ marginTop: space.lg, backgroundColor: colors.card, borderRadius: 16, padding: space.md }}>
        <Field label="Organisation name" value={organisationName} onChangeText={setOrganisationName} />
        <Field label="Your name" value={fullName} onChangeText={setFullName} />
        <Field label="Email" autoCapitalize="none" keyboardType="email-address" value={email} onChangeText={setEmail} />
        <Field label="Password (min 8 characters)" secureTextEntry value={password} onChangeText={setPassword} />
        <Field label="Town" value={town} onChangeText={setTown} />
        <Field label="Postcode" autoCapitalize="characters" value={postcode} onChangeText={setPostcode} />
        <Button label={busy ? 'Creating…' : 'Create organisation'} onPress={onSubmit} disabled={busy} />
        <Link href="/(auth)/login" style={{ marginTop: space.md, color: colors.amber, fontWeight: '700' }}>
          Already have an account? Sign in
        </Link>
      </View>
    </ScrollScreen>
  );
}
