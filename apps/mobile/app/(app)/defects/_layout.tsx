import { Stack } from 'expo-router';
import { colors } from '../../../src/theme';

export default function DefectsStack() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.navy },
        headerTintColor: colors.paper,
        headerTitleStyle: { fontWeight: '800' }
      }}
    >
      <Stack.Screen name="index" options={{ title: 'Defects' }} />
      <Stack.Screen name="new" options={{ title: 'Raise defect' }} />
      <Stack.Screen name="[id]" options={{ title: 'Defect' }} />
    </Stack>
  );
}
