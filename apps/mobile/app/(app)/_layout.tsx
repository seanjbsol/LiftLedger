import { Tabs } from 'expo-router';
import { Text } from 'react-native';
import { colors } from '../../src/theme';

function TabLabel({ label, focused }: { label: string; focused: boolean }) {
  return (
    <Text style={{ fontSize: 11, fontWeight: '800', color: focused ? colors.amber : '#9AA8B6', marginBottom: 4 }}>
      {label}
    </Text>
  );
}

export default function AppTabs() {
  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.navy },
        headerTintColor: colors.paper,
        headerTitleStyle: { fontWeight: '800' },
        tabBarStyle: { backgroundColor: colors.navy, borderTopColor: colors.navyMid, height: 58 },
        tabBarActiveTintColor: colors.amber,
        tabBarInactiveTintColor: '#9AA8B6',
        tabBarShowLabel: true,
        tabBarIcon: () => null
      }}
    >
      <Tabs.Screen
        name="index"
        options={{ title: 'Home', tabBarLabel: ({ focused }) => <TabLabel label="Home" focused={focused} /> }}
      />
      <Tabs.Screen
        name="assets"
        options={{
          headerShown: false,
          title: 'Assets',
          tabBarLabel: ({ focused }) => <TabLabel label="Assets" focused={focused} />
        }}
      />
      <Tabs.Screen
        name="inspections"
        options={{
          headerShown: false,
          title: 'Examinations',
          tabBarLabel: ({ focused }) => <TabLabel label="Exams" focused={focused} />
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{ title: 'Settings', tabBarLabel: ({ focused }) => <TabLabel label="Settings" focused={focused} /> }}
      />
    </Tabs>
  );
}
