// Float32 little-endian ↔ base64 (el mismo formato de las muestras del canal de entrenamiento).
export function floatsABase64(f32) {
  const bytes = new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength);
  let s = '';
  const paso = 0x8000;
  for (let i = 0; i < bytes.length; i += paso) s += String.fromCharCode.apply(null, bytes.subarray(i, i + paso));
  return btoa(s);
}

export function base64AFloats(b64) {
  const s = atob(b64);
  const bytes = new Uint8Array(s.length);
  for (let i = 0; i < s.length; i++) bytes[i] = s.charCodeAt(i);
  if (bytes.length % 4 !== 0) throw new Error('Datos de puntos clave dañados');
  return new Float32Array(bytes.buffer);
}
