import { useRouter } from 'expo-router';
import { useEffect } from 'react';
import { useLocalSearchParams } from 'expo-router';

export default function DeepLinkAsset() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const router = useRouter();

  useEffect(() => {
    if (code) {
      router.replace(`/(app)/assets/code/${encodeURIComponent(code)}`);
    } else {
      router.replace('/(app)/assets');
    }
  }, [code, router]);

  return null;
}
