// Arma la frase a partir de las señas reconocidas, una por una.
export class ConstructorFrase {
  constructor() {
    this.umbralConfianza = 0.6; // desde aquí la seña se da por buena
    this.umbralMinimo = 0.35;   // por debajo se ignora; entre ambos se muestra como dudosa
    this.fusionRepetidaMs = 500;
    this.maxSenas = 30;
    this.senas = [];
  }

  /** @param r resultado del reconocedor. Devuelve la seña agregada o null si no se aceptó. */
  agregar(r, inicioMs, finMs) {
    const c = r.candidatas[0];
    if (!c || c.prob < this.umbralMinimo) return null;
    const ultima = this.senas[this.senas.length - 1];
    if (ultima && ultima.glosa === c.glosa && inicioMs - ultima.finMs < this.fusionRepetidaMs) {
      ultima.finMs = finMs;
      ultima.prob = Math.max(ultima.prob, c.prob);
      ultima.dudosa = ultima.prob < this.umbralConfianza;
      return ultima;
    }
    const s = {
      glosa: c.glosa, prob: c.prob, dudosa: c.prob < this.umbralConfianza, inicioMs, finMs,
      alternativas: r.candidatas.slice(1, 4).filter((a) => a.prob >= 0.03).map((a) => ({ glosa: a.glosa, prob: a.prob })),
    };
    this.senas.push(s);
    if (this.senas.length > this.maxSenas) this.senas.shift();
    return s;
  }

  corregir(indice, glosa) {
    const s = this.senas[indice];
    if (!s) return;
    s.alternativas = s.alternativas.filter((a) => a.glosa !== glosa).concat([{ glosa: s.glosa, prob: s.prob }]);
    s.glosa = glosa;
    s.prob = 1;
    s.dudosa = false;
    s.corregida = true;
  }

  quitar(indice) { this.senas.splice(indice, 1); }
  deshacer() { this.senas.pop(); }
  limpiar() { this.senas = []; }
  get glosas() { return this.senas.map((s) => s.glosa); }
  get vacia() { return this.senas.length === 0; }
  get tieneDudas() { return this.senas.some((s) => s.dudosa); }
  get confianzaMedia() { return this.senas.length ? this.senas.reduce((a, s) => a + s.prob, 0) / this.senas.length : 0; }
  get finMs() { return this.senas.length ? this.senas[this.senas.length - 1].finMs : 0; }
}
