// Carga los datos sintéticos de prueba (enteros de 16 bits → Float32Array).
import { readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';

export function q16AFloat(b64, escala) {
  const buf = Buffer.from(b64, 'base64');
  const i16 = new Int16Array(buf.buffer, buf.byteOffset, buf.byteLength / 2);
  const f = new Float32Array(i16.length);
  for (let i = 0; i < i16.length; i++) f[i] = i16[i] / escala;
  return f;
}

export function cargar(ruta) {
  const d = JSON.parse(gunzipSync(readFileSync(ruta)).toString('utf8'));
  if (d.muestras) for (const m of d.muestras) m.sec = q16AFloat(m.sec, d.escala);
  if (d.tomas) for (const t of d.tomas) t.cuadros = q16AFloat(t.cuadros, d.escala);
  return d;
}

export const agrupar = (lista, clave) => {
  const m = new Map();
  for (const x of lista) { const k = clave(x); if (!m.has(k)) m.set(k, []); m.get(k).push(x); }
  return m;
};
