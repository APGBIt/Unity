// De puntos clave a rasgos comparables entre tomas y entre personas.
//
// Cada cuadro se describe por lo que distingue una seña de otra: dónde está cada
// mano respecto al cuerpo y qué forma y orientación tiene. Todo se mide en
// "anchos de hombro" o "tamaños de mano", así no depende de la distancia a la
// cámara, del encuadre ni de la talla de la persona.
import * as F from './formato.g.js';
import { presente } from './segmentacion.js';

const V = F.VALORES_POR_CUADRO;
export const DIM_MANO = 1 + 2 + F.PUNTOS_MANO * 3; // presente, posición (x, y), forma (21 × 3)
export const DIM = DIM_MANO * 2;
export const PESO_Z = 0.5; // la profundidad que da el detector es la más ruidosa

function dist2(c, o, ia, ib) {
  const dx = c[o + ia * 3] - c[o + ib * 3];
  const dy = c[o + ia * 3 + 1] - c[o + ib * 3 + 1];
  return Math.sqrt(dx * dx + dy * dy);
}

/**
 * Secuencia [T × 234] → rasgos [T × DIM].
 * El centro y el ancho de hombros se toman de toda la secuencia (mediana), porque
 * los hombros casi no se mueven al firmar y el detector los pierde a ratos.
 */
export function extraer(sec, t = sec.length / V) {
  const anchos = [], cxs = [], cys = [], manos = [];
  for (let i = 0; i < t; i++) {
    const o = i * V;
    const c = sec.subarray(o, o + V);
    if (presente(c, F.IDX_HOMBRO_IZQ) && presente(c, F.IDX_HOMBRO_DER)) {
      const a = dist2(sec, o, F.IDX_HOMBRO_IZQ, F.IDX_HOMBRO_DER);
      if (a > 1e-4) {
        anchos.push(a);
        cxs.push((sec[o + F.IDX_HOMBRO_IZQ * 3] + sec[o + F.IDX_HOMBRO_DER * 3]) / 2);
        cys.push((sec[o + F.IDX_HOMBRO_IZQ * 3 + 1] + sec[o + F.IDX_HOMBRO_DER * 3 + 1]) / 2);
      }
    }
    for (const ini of [F.INICIO_MANO_IZQ, F.INICIO_MANO_DER]) {
      if (presente(c, ini + F.MANO_MUNECA) && presente(c, ini + F.MANO_BASE_MEDIO)) manos.push(dist2(sec, o, ini + F.MANO_MUNECA, ini + F.MANO_BASE_MEDIO));
    }
  }
  const mediana = (a) => {
    if (!a.length) return 0;
    const b = a.slice().sort((x, y) => x - y), m = b.length >> 1;
    return b.length % 2 ? b[m] : (b[m - 1] + b[m]) / 2;
  };
  const hayCuerpo = anchos.length > 0;
  // Sin hombros a la vista (plano muy cerrado) no hay posición fiable: se compara solo la forma.
  const ancho = hayCuerpo ? mediana(anchos) : 1;
  const cx = hayCuerpo ? mediana(cxs) : 0, cy = hayCuerpo ? mediana(cys) : 0;
  const tamRef = mediana(manos);

  const r = new Float32Array(t * DIM);
  for (let i = 0; i < t; i++) {
    const o = i * V;
    [F.INICIO_MANO_IZQ, F.INICIO_MANO_DER].forEach((ini, h) => {
      const m = o + (ini + F.MANO_MUNECA) * 3;
      const c = sec.subarray(o, o + V);
      if (!presente(c, ini + F.MANO_MUNECA)) return;
      const d = i * DIM + h * DIM_MANO;
      r[d] = 1;
      if (hayCuerpo) {
        r[d + 1] = (sec[m] - cx) / ancho;
        r[d + 2] = (sec[m + 1] - cy) / ancho;
      }
      let tam = dist2(sec, o, ini + F.MANO_MUNECA, ini + F.MANO_BASE_MEDIO);
      if (tam < tamRef * 0.5) tam = tamRef; // mano de canto: el tamaño aparente engaña
      if (tam < 1e-5) tam = 1e-5;
      for (let k = 0; k < F.PUNTOS_MANO; k++) {
        const p = o + (ini + k) * 3;
        r[d + 3 + k * 3] = (sec[p] - sec[m]) / tam;
        r[d + 3 + k * 3 + 1] = (sec[p + 1] - sec[m + 1]) / tam;
        r[d + 3 + k * 3 + 2] = ((sec[p + 2] - sec[m + 2]) / tam) * PESO_Z;
      }
    });
  }
  return r;
}

/**
 * Los mismos rasgos como si la persona hubiera firmado con la otra mano:
 * intercambia las manos e invierte la x. Sale directo de los rasgos, sin volver a los puntos.
 */
export function espejarRasgos(r) {
  const t = r.length / DIM;
  const s = new Float32Array(r.length);
  for (let i = 0; i < t; i++) {
    for (let h = 0; h < 2; h++) {
      const a = i * DIM + h * DIM_MANO, b = i * DIM + (1 - h) * DIM_MANO;
      s[b] = r[a];
      s[b + 1] = -r[a + 1];
      s[b + 2] = r[a + 2];
      for (let k = 0; k < F.PUNTOS_MANO; k++) {
        s[b + 3 + k * 3] = -r[a + 3 + k * 3];
        s[b + 3 + k * 3 + 1] = r[a + 3 + k * 3 + 1];
        s[b + 3 + k * 3 + 2] = r[a + 3 + k * 3 + 2];
      }
    }
  }
  return s;
}

/** Diferencia entre dos cuadros de rasgos (0 = iguales). */
export function costoCuadro(a, ia, b, ib, pesos) {
  let total = 0;
  for (let h = 0; h < 2; h++) {
    const oa = ia * DIM + h * DIM_MANO, ob = ib * DIM + h * DIM_MANO;
    const pa = a[oa] > 0, pb = b[ob] > 0;
    if (!pa && !pb) continue;
    if (pa !== pb) { total += pesos.ausente; continue; }
    const dx = a[oa + 1] - b[ob + 1], dy = a[oa + 2] - b[ob + 2];
    let f = 0;
    for (let k = 3; k < DIM_MANO; k++) { const d = a[oa + k] - b[ob + k]; f += d * d; }
    total += pesos.posicion * (dx * dx + dy * dy) + pesos.forma * (f / F.PUNTOS_MANO);
  }
  return total;
}
