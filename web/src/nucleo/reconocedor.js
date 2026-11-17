// Reconocedor por ejemplos: aprende cada seña con unas pocas grabaciones hechas en el propio teléfono.
//
// Por qué existe: un modelo entrenado necesita cientos de grabaciones de muchas
// personas, y de la LSRD todavía no hay ninguna pública. Comparar contra ejemplos
// funciona desde la primera seña enseñada, se adapta a la persona que firma y
// nunca "inventa" una seña que nadie le enseñó.
//
// Cómo compara: alinea la seña nueva con cada ejemplo cuadro a cuadro permitiendo
// que una sea algo más rápida o más lenta que la otra (alineamiento temporal
// dinámico), y mide cuánto difieren posición y forma de las manos.
import { LONGITUD_SECUENCIA } from './formato.g.js';
import { DIM, costoCuadro, espejarRasgos, extraer } from './rasgos.js';

export const OPCIONES = {
  banda: 5,               // cuánto puede adelantarse o atrasarse una seña respecto del ejemplo (en cuadros de 32)
  pesos: { posicion: 1.0, forma: 2.5, ausente: 1.2 },
  vecinos: 2,             // la distancia a una seña es el promedio de sus ejemplos más parecidos
  aceptarEspejo: true,    // reconocer también a quien firma con la otra mano
  radioMinimo: 0.10,      // variación mínima que se le supone a una seña con ejemplos casi idénticos
  radioPorDefecto: 0.22,  // para señas con un solo ejemplo
  factorDesconocida: 2.4, // a partir de cuántas veces su variación normal una seña "no se parece a nada"
  temperatura: 0.45,
  minEjemplos: 1,
};

const T = LONGITUD_SECUENCIA;

/** Distancia entre dos secuencias de rasgos (raíz del costo medio por paso del mejor alineamiento). */
export function distancia(a, b, op = OPCIONES, corte = Infinity) {
  const r = op.banda;
  const INF = Infinity;
  let previo = new Float64Array(T + 1).fill(INF), pasosPrevio = new Int32Array(T + 1);
  let actual = new Float64Array(T + 1).fill(INF), pasosActual = new Int32Array(T + 1);
  previo[0] = 0;
  const corte2 = corte === Infinity ? Infinity : corte * corte;
  for (let i = 1; i <= T; i++) {
    actual.fill(INF);
    const j0 = Math.max(1, i - r), j1 = Math.min(T, i + r);
    let minFila = INF;
    for (let j = j0; j <= j1; j++) {
      const c = costoCuadro(a, i - 1, b, j - 1, op.pesos);
      let mejor = previo[j - 1], pasos = pasosPrevio[j - 1];
      if (previo[j] < mejor) { mejor = previo[j]; pasos = pasosPrevio[j]; }
      if (actual[j - 1] < mejor) { mejor = actual[j - 1]; pasos = pasosActual[j - 1]; }
      actual[j] = mejor + c;
      pasosActual[j] = pasos + 1;
      const media = actual[j] / pasosActual[j];
      if (media < minFila) minFila = media;
    }
    // Si ni el mejor camino parcial puede quedar bajo el corte, no vale la pena seguir.
    if (corte2 !== Infinity && i > T / 2 && minFila * (i / T) > corte2 * 1.5) return Infinity;
    [previo, actual] = [actual, previo];
    [pasosPrevio, pasosActual] = [pasosActual, pasosPrevio];
  }
  return Math.sqrt(previo[T] / pasosPrevio[T]);
}

const mediana = (a) => { const b = a.slice().sort((x, y) => x - y); return b.length ? b[b.length >> 1] : 0; };

export class ReconocedorPorEjemplos {
  constructor(opciones = {}) {
    this.op = { ...OPCIONES, ...opciones, pesos: { ...OPCIONES.pesos, ...(opciones.pesos || {}) } };
    this.clases = new Map(); // glosa → { ejemplos: [rasgos], radio }
  }

  get glosas() { return [...this.clases.keys()]; }
  get total() { let n = 0; for (const c of this.clases.values()) n += c.ejemplos.length; return n; }
  cantidad(glosa) { return this.clases.get(glosa)?.ejemplos.length || 0; }

  /** Reemplaza todo lo aprendido. `ejemplos`: [{ glosa, sec: Float32Array(32 × 234) }]. */
  fijar(ejemplos) {
    this.clases.clear();
    for (const e of ejemplos) {
      if (!this.clases.has(e.glosa)) this.clases.set(e.glosa, { ejemplos: [], radio: this.op.radioPorDefecto });
      this.clases.get(e.glosa).ejemplos.push(extraer(e.sec));
    }
    this.calibrar();
  }

  /**
   * Mide cuánto varía cada seña entre sus propios ejemplos. Esa variación ("radio")
   * es la vara para decidir si una seña nueva se parece lo suficiente.
   */
  calibrar() {
    const radios = [];
    for (const c of this.clases.values()) {
      if (c.ejemplos.length < 2) { c.radio = null; continue; }
      const cercanas = c.ejemplos.map((e, i) => {
        let min = Infinity;
        c.ejemplos.forEach((o, j) => { if (i !== j) min = Math.min(min, distancia(e, o, this.op)); });
        return min;
      });
      c.radio = Math.max(this.op.radioMinimo, mediana(cercanas));
      radios.push(c.radio);
    }
    const tipico = radios.length ? mediana(radios) : this.op.radioPorDefecto;
    for (const c of this.clases.values()) {
      if (c.radio === null) c.radio = Math.max(tipico, this.op.radioPorDefecto);
      // Con solo dos ejemplos la variación medida depende de un único par: no se le deja pasar de la típica.
      else if (c.ejemplos.length === 2 && radios.length > 2) c.radio = Math.min(c.radio, tipico * 1.25);
    }
    this.radioTipico = tipico;
  }

  /**
   * Compara una seña nueva con todo lo aprendido.
   * Devuelve { candidatas: [{ glosa, prob, distancia }], ninguna, espejo }.
   * `ninguna` es la probabilidad de que no sea ninguna de las señas enseñadas.
   */
  clasificar(sec) {
    if (this.clases.size === 0) return { candidatas: [], ninguna: 1, espejo: false };
    const directa = extraer(sec);
    const a = this.puntuar(directa);
    let mejor = a, espejo = false;
    if (this.op.aceptarEspejo) {
      const b = this.puntuar(espejarRasgos(directa));
      // El espejo solo gana si es claramente mejor: evita confundir señas que son espejo una de otra.
      if (b[0].relativa < a[0].relativa * 0.8) { mejor = b; espejo = true; }
    }
    const temp = this.op.temperatura;
    const pesos = mejor.map((c) => Math.exp(-c.relativa / temp));
    const pesoNinguna = Math.exp(-this.op.factorDesconocida / temp);
    const suma = pesos.reduce((s, x) => s + x, 0) + pesoNinguna;
    const candidatas = mejor.map((c, i) => ({ glosa: c.glosa, prob: pesos[i] / suma, distancia: c.d }));
    return { candidatas: candidatas.slice(0, 5), ninguna: pesoNinguna / suma, espejo };
  }

  /** Distancia a cada seña, en bruto (`d`) y medida en radios de esa seña (`relativa`), de menor a mayor. */
  puntuar(rasgos) {
    const salida = [];
    for (const [glosa, c] of this.clases) {
      if (c.ejemplos.length < this.op.minEjemplos) continue;
      const ds = c.ejemplos.map((e) => distancia(rasgos, e, this.op)).sort((x, y) => x - y);
      const k = Math.min(this.op.vecinos, ds.length);
      let d = 0;
      for (let i = 0; i < k; i++) d += ds[i];
      d /= k;
      salida.push({ glosa, d, relativa: d / c.radio });
    }
    return salida.sort((x, y) => x.relativa - y.relativa);
  }
}

export { DIM };
