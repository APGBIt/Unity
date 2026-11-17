// De la salida del detector holístico de MediaPipe al formato LSRD-LM v1 (igual que EmpaquetadorLandmarks.cs).
import * as F from './formato.g.js';
import { presente } from './segmentacion.js';

export const UMBRAL_VISIBILIDAD_POSE = 0.5;

function poner(d, idx, x, y, z, aspecto) {
  d[idx * 3] = x * aspecto;
  d[idx * 3 + 1] = y;
  d[idx * 3 + 2] = z * aspecto;
}

/**
 * @param pose  lista de 33 puntos {x,y,z,visibility} o null
 * @param manoIzq, manoDer  listas de 21 puntos {x,y,z} o null (izquierda y derecha de la persona)
 * @param cara  lista de ≥468 puntos o null
 * @param aspecto  ancho / alto de la imagen procesada
 */
export function empaquetar(pose, manoIzq, manoDer, cara, aspecto, destino) {
  const d = destino || new Float32Array(F.VALORES_POR_CUADRO);
  d.fill(0);
  if (pose && pose.length >= 33) {
    F.POSE_INDICES_MEDIAPIPE.forEach((m, k) => {
      const p = pose[m];
      const vis = p.visibility;
      if (vis === undefined || vis === null || Number.isNaN(vis) || vis >= UMBRAL_VISIBILIDAD_POSE) poner(d, F.INICIO_POSE + k, p.x, p.y, p.z || 0, aspecto);
    });
  }
  const mano = (inicio, puntos) => {
    if (!puntos || puntos.length < F.PUNTOS_MANO) return;
    for (let k = 0; k < F.PUNTOS_MANO; k++) poner(d, inicio + k, puntos[k].x, puntos[k].y, puntos[k].z || 0, aspecto);
  };
  mano(F.INICIO_MANO_IZQ, manoIzq);
  mano(F.INICIO_MANO_DER, manoDer);
  if (cara && cara.length >= 468) {
    F.CARA_INDICES_MEDIAPIPE.forEach((m, k) => poner(d, F.INICIO_CARA + k, cara[m].x, cara[m].y, cara[m].z || 0, aspecto));
  }
  return d;
}

/** Espejo horizontal de un cuadro: invierte x e intercambia izquierda ↔ derecha. */
export function espejar(cuadro) {
  const s = new Float32Array(F.VALORES_POR_CUADRO);
  for (let i = 0; i < F.N_PUNTOS; i++) {
    const o = F.PERMUTACION_ESPEJO[i];
    if (!presente(cuadro, o)) continue;
    s[i * 3] = -cuadro[o * 3];
    s[i * 3 + 1] = cuadro[o * 3 + 1];
    s[i * 3 + 2] = cuadro[o * 3 + 2];
  }
  return s;
}

/**
 * Mano que deletrea: la visible con la muñeca más alta (empate: la derecha), canonizada
 * como mano derecha. Devuelve null si no hay manos.
 */
export function manoActiva(cuadro) {
  const iz = F.INICIO_MANO_IZQ, de = F.INICIO_MANO_DER;
  const hayI = presente(cuadro, iz + F.MANO_MUNECA), hayD = presente(cuadro, de + F.MANO_MUNECA);
  if (!hayI && !hayD) return null;
  const usarIzq = hayI && hayD ? cuadro[(iz + F.MANO_MUNECA) * 3 + 1] < cuadro[(de + F.MANO_MUNECA) * 3 + 1] : hayI;
  const inicio = usarIzq ? iz : de;
  const s = new Float32Array(F.PUNTOS_MANO * 3);
  s.set(cuadro.subarray(inicio * 3, (inicio + F.PUNTOS_MANO) * 3));
  if (usarIzq) for (let k = 0; k < F.PUNTOS_MANO; k++) s[k * 3] = -s[k * 3];
  return s;
}
