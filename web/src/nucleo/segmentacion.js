// Segmentación de señas en tiempo real: dónde empieza y dónde termina cada seña.
// Copia exacta de ml/lsrd_ml/segmentacion.py (las pruebas comparan ambas con los mismos datos).
import * as F from './formato.g.js';

export const REPOSO = 0, POSIBLE = 1, SENA = 2, ESPERA = 3;
export const parametrosPorDefecto = () => ({ ...F.SEGMENTACION });

export function presente(c, idx) {
  const o = idx * 3;
  return Math.abs(c[o]) + Math.abs(c[o + 1]) + Math.abs(c[o + 2]) > 0;
}

function dist2d(a, ia, b, ib) {
  const dx = a[ia * 3] - b[ib * 3];
  const dy = a[ia * 3 + 1] - b[ib * 3 + 1];
  return Math.sqrt(dx * dx + dy * dy);
}

/** Ancho de hombros (o su estimación a partir de la mano). 0 si no hay referencia. */
export function anchoReferencia(c, p) {
  if (presente(c, F.IDX_HOMBRO_IZQ) && presente(c, F.IDX_HOMBRO_DER)) return dist2d(c, F.IDX_HOMBRO_IZQ, c, F.IDX_HOMBRO_DER);
  for (const inicio of [F.INICIO_MANO_IZQ, F.INICIO_MANO_DER]) {
    if (presente(c, inicio + F.MANO_MUNECA) && presente(c, inicio + F.MANO_BASE_MEDIO)) {
      return p.factor_mano_a_hombros * dist2d(c, inicio + F.MANO_MUNECA, c, inicio + F.MANO_BASE_MEDIO);
    }
  }
  return 0;
}

/** Devuelve [activo, velocidad]; velocidad en anchos de hombro por segundo. */
export function medirCuadro(actual, anterior, dtS, p) {
  const munecas = [F.IDX_MUNECA_IZQ, F.IDX_MUNECA_DER];
  const presentes = munecas.filter((m) => presente(actual, m));
  if (presentes.length === 0) return [false, 0];

  const hombros = presente(actual, F.IDX_HOMBRO_IZQ) && presente(actual, F.IDX_HOMBRO_DER);
  let activo = false;
  if (hombros) {
    const ancho = dist2d(actual, F.IDX_HOMBRO_IZQ, actual, F.IDX_HOMBRO_DER);
    const yHombros = (actual[F.IDX_HOMBRO_IZQ * 3 + 1] + actual[F.IDX_HOMBRO_DER * 3 + 1]) / 2;
    for (const m of presentes) {
      if (ancho < 1e-6 || actual[m * 3 + 1] < yHombros + p.linea_reposo * ancho) activo = true;
    }
  } else {
    activo = true;
  }

  let velocidad = Infinity;
  const escala = anchoReferencia(actual, p);
  if (anterior && dtS > 0 && escala > 1e-6) {
    let vmax = -1;
    for (const m of munecas) {
      if (presente(actual, m) && presente(anterior, m)) {
        const v = dist2d(actual, m, anterior, m) / escala / dtS;
        if (v > vmax) vmax = v;
      }
    }
    if (vmax >= 0) velocidad = vmax;
  }
  return [activo, velocidad];
}

/** Máquina de estados que emite segmentos [tInicioMs, tFinMs]. */
export class Segmentador {
  constructor(parametros) {
    this.p = parametros || parametrosPorDefecto();
    this.reiniciar();
  }

  reiniciar() {
    this.estado = REPOSO;
    this.tInicio = 0;
    this.tUltimoActivo = 0;
    this.tQuieto = null;
    this.tInactivo = null;
  }

  emitir(tIni, tFin) {
    return tFin - tIni >= this.p.min_ms ? [tIni, tFin] : null;
  }

  procesar(tMs, activo, velocidad) {
    const p = this.p;
    if (this.estado === REPOSO) {
      if (activo) { this.estado = POSIBLE; this.tInicio = tMs; }
      return null;
    }
    if (this.estado === POSIBLE) {
      if (!activo) { this.estado = REPOSO; return null; }
      if (tMs - this.tInicio >= p.inicio_ms) { this.estado = SENA; this.tUltimoActivo = tMs; this.tQuieto = null; }
      return null;
    }
    if (this.estado === SENA) {
      if (activo) {
        this.tUltimoActivo = tMs;
        if (p.usar_quietud && velocidad < p.umbral_velocidad) {
          if (this.tQuieto === null) this.tQuieto = tMs;
          else if (tMs - this.tQuieto >= p.quietud_ms && tMs - this.tInicio >= p.min_ms) {
            const seg = this.emitir(this.tInicio, tMs);
            this.estado = ESPERA;
            this.tInactivo = null;
            return seg;
          }
        } else {
          this.tQuieto = null;
        }
        if (tMs - this.tInicio >= p.max_ms) {
          const seg = this.emitir(this.tInicio, tMs);
          this.tInicio = tMs;
          this.tQuieto = null;
          return seg;
        }
        return null;
      }
      if (tMs - this.tUltimoActivo >= p.fin_ms) {
        this.estado = REPOSO;
        return this.emitir(this.tInicio, this.tUltimoActivo);
      }
      return null;
    }
    // ESPERA: tras una seña estática hay que moverse o bajar las manos
    if (activo) {
      this.tInactivo = null;
      if (velocidad >= p.umbral_velocidad) { this.estado = POSIBLE; this.tInicio = tMs; }
    } else if (this.tInactivo === null) {
      this.tInactivo = tMs;
    } else if (tMs - this.tInactivo >= p.fin_ms) {
      this.estado = REPOSO;
    }
    return null;
  }

  /** Fin del flujo: emite la seña en curso, si la hay. */
  cerrar() {
    const seg = this.estado === SENA ? this.emitir(this.tInicio, this.tUltimoActivo) : null;
    this.reiniciar();
    return seg;
  }
}

export function segmentarSecuencia(cuadros, tiemposMs, parametros) {
  const p = parametros || parametrosPorDefecto();
  const seg = new Segmentador(p);
  const salida = [];
  const mediciones = [];
  let anterior = null, tAnt = null;
  for (let i = 0; i < cuadros.length; i++) {
    const t = tiemposMs[i];
    const dt = tAnt !== null ? (t - tAnt) / 1000 : 0;
    const [activo, vel] = medirCuadro(cuadros[i], anterior, dt, p);
    mediciones.push([activo, vel]);
    const r = seg.procesar(t, activo, vel);
    if (r) salida.push(r);
    anterior = cuadros[i];
    tAnt = t;
  }
  const r = seg.cerrar();
  if (r) salida.push(r);
  return { segmentos: salida, mediciones };
}

/**
 * Segmentador con memoria de cuadros para uso en vivo: recibe cuadro a cuadro
 * y devuelve la lista de cuadros de cada seña terminada.
 */
export class SegmentadorEnVivo {
  constructor(parametros, margenMs = 60) {
    this.p = parametros || parametrosPorDefecto();
    this.seg = new Segmentador(this.p);
    this.margenMs = margenMs;
    this.buffer = [];
    this.anterior = null;
    this.tAnt = null;
    this.activo = false;
  }

  reiniciar() {
    this.seg.reiniciar();
    this.buffer = [];
    this.anterior = null;
    this.tAnt = null;
  }

  get enSena() { return this.seg.estado === SENA; }

  /** Devuelve null o { cuadros, tiempos, inicioMs, finMs }. */
  agregar(cuadro, tMs) {
    const dt = this.tAnt !== null ? (tMs - this.tAnt) / 1000 : 0;
    const [activo, vel] = medirCuadro(cuadro, this.anterior, dt, this.p);
    this.activo = activo;
    this.buffer.push({ t: tMs, c: cuadro });
    const limite = tMs - this.p.max_ms - 1500;
    while (this.buffer.length > 2 && this.buffer[0].t < limite) this.buffer.shift();
    const r = this.seg.procesar(tMs, activo, vel);
    this.anterior = cuadro;
    this.tAnt = tMs;
    if (!r) return null;
    const [ini, fin] = r;
    const sel = this.buffer.filter((b) => b.t >= ini - this.margenMs && b.t <= fin + this.margenMs);
    if (sel.length < 4) return null;
    return { cuadros: sel.map((b) => b.c), tiempos: sel.map((b) => b.t), inicioMs: ini, finMs: fin };
  }
}
