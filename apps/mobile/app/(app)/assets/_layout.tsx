import { Stack } from 'expo-router';
import { colors } from '../../../src/theme';

export default function AssetsStack() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.navy },
        headerTintColor: colors.paper,
        headerTitleStyle: { fontWeight: '800' }
      }}
    >
      <Stack.Screen name="index" options={{ title: 'Assets' }} />
      <Stack.Screen name="new" options={{ title: 'Add asset' }} />
      <Stack.Screen name="[id]" options={{ title: 'Asset' }} />
      <Stack.Screen name="code/[code]" options={{ title: 'QR / ID code' }} />
    </Stack>
  );
}
