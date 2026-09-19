import { Stack } from 'expo-router';
import { colors } from '../../../src/theme';

export default function InspectionsStack() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.navy },
        headerTintColor: colors.paper,
        headerTitleStyle: { fontWeight: '800' }
      }}
    >
      <Stack.Screen name="index" options={{ title: 'Examinations' }} />
      <Stack.Screen name="new" options={{ title: 'Start examination' }} />
      <Stack.Screen name="[id]" options={{ title: 'Examination' }} />
    </Stack>
  );
}
