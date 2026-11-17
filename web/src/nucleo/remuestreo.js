// Remuestreo temporal a longitud fija por vecino más cercano (idéntico a remuestreo.py y Remuestreo.cs).
import { LONGITUD_SECUENCIA, VALORES_POR_CUADRO } from './formato.g.js';

export function indices(longitud, t = LONGITUD_SECUENCIA) {
  if (longitud <= 0) throw new Error('La secuencia está vacía');
  const salida = new Array(t);
  for (let i = 0; i < t; i++) salida[i] = Math.min(longitud - 1, Math.floor(((i + 0.5) * longitud) / t));
  return salida;
}

/** Lista de cuadros (Float32Array de 234) → un Float32Array de t × 234. */
export function remuestrear(cuadros, t = LONGITUD_SECUENCIA) {
  const idx = indices(cuadros.length, t);
  const salida = new Float32Array(t * VALORES_POR_CUADRO);
  for (let i = 0; i < t; i++) salida.set(cuadros[idx[i]], i * VALORES_POR_CUADRO);
  return salida;
}
