import { Text } from 'react-native';
import { API_URL } from '../../src/api';
import { useAuth } from '../../src/auth';
import { Banner, Button, Card, ScrollScreen, Subtitle, Title } from '../../src/ui';
import { colors } from '../../src/theme';

export default function SettingsScreen() {
  const { user, tenant, role, signOut } = useAuth();

  return (
    <ScrollScreen>
      <Title>Settings</Title>
      <Subtitle>Signed in to a single organisation tenant. JWT carries tenant_id and role.</Subtitle>
      <Card>
        <LabelValue label="Name" value={user.fullName} />
        <LabelValue label="Email" value={user.email} />
        <LabelValue label="Role" value={role} />
        <LabelValue label="Organisation" value={tenant.name} />
        <LabelValue label="API" value={API_URL} />
      </Card>
      <Banner text="Records are isolated by organisation. LiftLedger is a working record system and does not replace a competent person’s legal duties under LOLER or PUWER, and it is not HSE-certified." />
      <Button label="Sign out" tone="danger" onPress={() => void signOut()} />
    </ScrollScreen>
  );
}

function LabelValue({ label, value }: { label: string; value: string }) {
  return (
    <>
      <Text style={{ color: colors.muted, fontSize: 12, fontWeight: '700', marginTop: 8 }}>{label}</Text>
      <Text style={{ color: colors.ink, marginTop: 2 }}>{value}</Text>
    </>
  );
}
