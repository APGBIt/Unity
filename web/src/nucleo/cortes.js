// Corta las señas de una grabación completa (un video ya convertido en puntos clave).
import { IDX_MUNECA_DER, IDX_MUNECA_IZQ } from './formato.g.js';
import { presente, segmentarSecuencia } from './segmentacion.js';
import { remuestrear } from './remuestreo.js';

const conManos = (c) => presente(c, IDX_MUNECA_IZQ) || presente(c, IDX_MUNECA_DER);

/**
 * @returns [{ sec: Float32Array(32 × 234), duracionMs, inicioMs, finMs }]
 * Si el video empieza con las manos ya arriba y nunca bajan (clip recortado de un
 * diccionario), no hay pausas que marquen la seña: se toma todo el tramo con manos como una sola.
 */
export function cortarSenas(cuadros, tiempos, maximo = 8) {
  const salida = [];
  if (cuadros.length < 4) return salida;
  for (const [ini, fin] of segmentarSecuencia(cuadros, tiempos).segmentos) {
    const sel = cuadros.filter((c, i) => tiempos[i] >= ini && tiempos[i] <= fin);
    if (sel.length >= 4 && sel.filter(conManos).length >= sel.length * 0.5) salida.push({ sec: remuestrear(sel), duracionMs: fin - ini, inicioMs: ini, finMs: fin });
  }
  if (salida.length === 0) {
    const idx = cuadros.map((c, i) => (conManos(c) ? i : -1)).filter((i) => i >= 0);
    if (idx.length >= 4) {
      const a = idx[0], b = idx[idx.length - 1];
      const sel = cuadros.slice(a, b + 1);
      if (idx.length >= sel.length * 0.5) salida.push({ sec: remuestrear(sel), duracionMs: tiempos[b] - tiempos[a], inicioMs: tiempos[a], finMs: tiempos[b] });
    }
  }
  return salida.slice(0, maximo);
}
