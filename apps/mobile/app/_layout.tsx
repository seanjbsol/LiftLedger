import { Stack, useRouter, useSegments } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { AuthProvider, useAuth } from '../src/auth';
import { EntitlementsProvider } from '../src/entitlements';
import { colors } from '../src/theme';

function Gate() {
  const { token, ready } = useAuth();
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    if (!ready) {
      return;
    }
    const inAuth = segments[0] === '(auth)';
    const inDeepLink = segments[0] === 'a';
    if (!token && !inAuth) {
      router.replace('/(auth)/login');
    } else if (token && inAuth) {
      router.replace('/(app)');
    } else if (!token && inDeepLink) {
      router.replace('/(auth)/login');
    }
  }, [ready, token, segments, router]);

  if (!ready) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.navy }}>
        <ActivityIndicator color={colors.amber} />
      </View>
    );
  }

  return (
    <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.paper } }}>
      <Stack.Screen name="(auth)" />
      <Stack.Screen name="(app)" />
      <Stack.Screen name="a/[code]" />
    </Stack>
  );
}

export default function RootLayout() {
  return (
    <AuthProvider>
      <EntitlementsProvider>
        <StatusBar style="light" />
        <Gate />
      </EntitlementsProvider>
    </AuthProvider>
  );
}
