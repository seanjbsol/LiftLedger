export function pickImage(): Promise<{ file: Blob; name: string; type: string } | null> {
  if (typeof document === 'undefined') {
    return Promise.resolve(null);
  }

  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/jpeg,image/png,image/webp';
    input.onchange = () => {
      const file = input.files?.[0];
      if (!file) {
        resolve(null);
        return;
      }
      resolve({ file, name: file.name, type: file.type || 'image/jpeg' });
    };
    input.click();
  });
}
