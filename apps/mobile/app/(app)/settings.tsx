import { useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { Linking, Text } from 'react-native';
import { API_URL, api, ApiError } from '../../src/api';
import { useAuth } from '../../src/auth';
import type { BillingEntitlements } from '../../src/types';
import { Banner, Button, Card, ScrollScreen, Subtitle, Title } from '../../src/ui';
import { colors } from '../../src/theme';

export default function SettingsScreen() {
  const { user, tenant, role, signOut } = useAuth();
  const canManageBilling = role === 'Owner' || role === 'Admin';
  const [billing, setBilling] = useState<BillingEntitlements | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const entitlements = await api.entitlements();
      setBilling(entitlements);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load billing status.');
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const openBilling = async (kind: 'checkout' | 'portal') => {
    setBusy(true);
    setError(null);
    try {
      const session = kind === 'portal' ? await api.portal() : await api.checkout();
      await Linking.openURL(session.url);
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : err instanceof Error ? err.message : 'Billing request failed.';
      setError(message);
    } finally {
      setBusy(false);
    }
  };

  const statusLabel = billing ? formatStatus(billing.status) : 'Loading…';
  const planLabel = billing?.planName || billing?.planCode || 'Not selected';

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
      <Card>
        <LabelValue label="Plan" value={planLabel} />
        <LabelValue label="Subscription" value={statusLabel} />
        {billing?.currentPeriodEnd ? (
          <LabelValue label="Current period ends" value={new Date(billing.currentPeriodEnd).toLocaleDateString('en-GB')} />
        ) : null}
        {canManageBilling ? (
          <>
            <Button
              label={billing?.hasAccess ? 'Manage billing' : 'Upgrade'}
              onPress={() => void openBilling(billing?.hasAccess ? 'portal' : 'checkout')}
              disabled={busy}
            />
            {billing?.hasAccess ? (
              <Button label="Change plan" tone="secondary" onPress={() => void openBilling('checkout')} disabled={busy} />
            ) : null}
          </>
        ) : (
          <Banner text="Ask an organisation owner or admin to manage billing." />
        )}
      </Card>
      {error ? <Banner tone="danger" text={error} /> : null}
      <Banner text="Records are isolated by organisation. LiftLedger is a working record system and does not replace a competent person’s legal duties under LOLER or PUWER, and it is not HSE-certified." />
      <Button label="Sign out" tone="danger" onPress={() => void signOut()} />
    </ScrollScreen>
  );
}

function formatStatus(status: string) {
  const normalised = status.replace(/_/g, ' ');
  return normalised ? normalised.charAt(0).toUpperCase() + normalised.slice(1) : status;
}

function LabelValue({ label, value }: { label: string; value: string }) {
  return (
    <>
      <Text style={{ color: colors.muted, fontSize: 12, fontWeight: '700', marginTop: 8 }}>{label}</Text>
      <Text style={{ color: colors.ink, marginTop: 2 }}>{value}</Text>
    </>
  );
}
