import { useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { Linking, Text } from 'react-native';
import { API_URL, api, ApiError } from '../../src/api';
import { useAuth } from '../../src/auth';
import { useEntitlements } from '../../src/entitlements';
import type { BillingEntitlements, ClientDto, PortalToken } from '../../src/types';
import { Banner, Button, Card, ScrollScreen, Subtitle, Title, UpgradeGate } from '../../src/ui';
import { colors } from '../../src/theme';

export default function SettingsScreen() {
  const { user, tenant, role, signOut } = useAuth();
  const { billing, refresh, canUseClientPortal } = useEntitlements();
  const canManageBilling = role === 'Owner' || role === 'Admin';
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [clients, setClients] = useState<ClientDto[]>([]);
  const [share, setShare] = useState<PortalToken | null>(null);

  useFocusEffect(
    useCallback(() => {
      void refresh();
      if (canUseClientPortal) {
        api.clients().then(setClients).catch(() => undefined);
      }
    }, [refresh, canUseClientPortal])
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

  const plan = billing as BillingEntitlements | null;
  const statusLabel = plan ? formatStatus(plan.status) : 'Loading…';
  const planLabel = plan?.planName || plan?.planCode || 'Not selected';
  const tier = plan?.planTier || 'Starter';

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
        <LabelValue label="Plan" value={`${planLabel} (${tier})`} />
        <LabelValue label="Subscription" value={statusLabel} />
        <LabelValue
          label="Included"
          value={
            plan?.isPro
              ? 'Pro: certificate builders, defect workflow, client download portal'
              : 'Starter: asset list and simple examination log'
          }
        />
        {plan?.currentPeriodEnd ? (
          <LabelValue label="Current period ends" value={new Date(plan.currentPeriodEnd).toLocaleDateString('en-GB')} />
        ) : null}
        {canManageBilling ? (
          <>
            <Button
              label={plan?.hasAccess ? (plan.isPro ? 'Manage billing' : 'Upgrade to Pro') : 'Upgrade'}
              onPress={() => void openBilling(plan?.hasAccess && plan.isPro ? 'portal' : 'checkout')}
              disabled={busy}
            />
            {plan?.hasAccess ? (
              <Button label="Change plan" tone="secondary" onPress={() => void openBilling('checkout')} disabled={busy} />
            ) : null}
          </>
        ) : (
          <Banner text="Ask an organisation owner or admin to manage billing." />
        )}
      </Card>
      <Card>
        <LabelValue label="Client download portal" value="Share last working records with a hire customer (Pro)." />
        {canUseClientPortal ? (
          clients.length === 0 ? (
            <Banner text="Add a client to create a download link." />
          ) : (
            <>
              {clients.map((client) => (
                <Button
                  key={client.id}
                  label={`Share link for ${client.name}`}
                  tone="secondary"
                  onPress={() =>
                    void (async () => {
                      try {
                        setShare(await api.createPortalToken(client.id));
                        setError(null);
                      } catch (err) {
                        setError(err instanceof Error ? err.message : 'Could not create a portal link.');
                      }
                    })()
                  }
                />
              ))}
              {share ? (
                <Banner
                  tone="success"
                  text={`${share.clientName}: ${API_URL}${share.publicUrl}`}
                />
              ) : null}
            </>
          )
        ) : (
          <UpgradeGate feature="Client download portal" />
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
