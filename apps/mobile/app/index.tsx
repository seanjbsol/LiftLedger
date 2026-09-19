import { Redirect } from 'expo-router';
import { useAuth } from '../src/auth';

export default function Index() {
  const { token, ready } = useAuth();
  if (!ready) {
    return null;
  }
  return <Redirect href={token ? '/(app)' : '/(auth)/login'} />;
}
