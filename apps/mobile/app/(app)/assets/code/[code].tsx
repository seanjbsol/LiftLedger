import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useState } from 'react';
import { api, ApiError } from '../../../../src/api';
import { useEntitlements } from '../../../../src/entitlements';
import type { AssetScan } from '../../../../src/types';
import {
  Banner,
  Button,
  Card,
  Loading,
  ScrollScreen,
  Subtitle,
  Title,
  UpgradeGate,
  formatDate
} from '../../../../src/ui';

export default function AssetCodeScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const router = useRouter();
  const { canUseCertificates } = useEntitlements();
  const [scan, setScan] = useState<AssetScan | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!code) {
      return;
    }
    api
      .assetByCode(code)
      .then(setScan)
      .catch((err) => setError(err instanceof Error ? err.message : 'No asset matched that code.'));
  }, [code]);

  const start = async () => {
    if (!scan) {
      return;
    }
    setBusy(true);
    try {
      const inspection = await api.startInspection(scan.id);
      router.replace(`/(app)/inspections/${inspection.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start examination.');
    } finally {
      setBusy(false);
    }
  };

  const openCert = async () => {
    if (!scan?.lastInspectionId) {
      return;
    }
    try {
      if (scan.lastCertificateId) {
        await api.openStoredCertificate(scan.lastCertificateId);
      } else {
        await api.openInspectionCertificate(scan.lastInspectionId);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 402) {
        setError(err.message);
      } else {
        setError(err instanceof Error ? err.message : 'Could not open the last working record.');
      }
    }
  };

  if (!scan) {
    return (
      <ScrollScreen>
        {error ? <Banner tone="danger" text={error} /> : <Loading />}
      </ScrollScreen>
    );
  }

  return (
    <ScrollScreen>
      <Title>{scan.assetNumber}</Title>
      <Subtitle>
        QR / ID {scan.identificationCode ?? scan.assetNumber}. Last working record or start a new examination.
      </Subtitle>
      {error ? <Banner tone="danger" text={error} /> : null}
      <Card>
        <Subtitle>
          {scan.name}
          {scan.lastCertificateNumber ? ` · last record ${scan.lastCertificateNumber}` : ''}
          {scan.lastExaminationDate ? ` · ${formatDate(scan.lastExaminationDate)}` : ''}
        </Subtitle>
      </Card>
      {scan.lastInspectionId ? (
        canUseCertificates ? (
          <Button label="Open last working record" onPress={() => void openCert()} />
        ) : (
          <UpgradeGate feature="Stored certificates" />
        )
      ) : null}
      <Button label={busy ? 'Starting…' : 'Start thorough examination'} tone="secondary" onPress={() => void start()} disabled={busy} />
      <Button label="Open asset" tone="secondary" onPress={() => router.replace(`/(app)/assets/${scan.id}`)} />
    </ScrollScreen>
  );
}
