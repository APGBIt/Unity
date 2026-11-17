// Vocabulario, diccionario y traducción literal sin conexión.
import { FUNCIONALES, PREGUNTAS, clave, leerCsv, normalizar, sinTildes } from './texto.js';

const NUM = /^NUM-(\d+)$/;

export class Vocabulario {
  /**
   * @param filas          filas de vocabulario_mvp.csv
   * @param cobertura      filas de cobertura_diccionario_oficial.csv (opcional)
   * @param propias        señas añadidas por la persona: [{ glosa, espanol }]
   */
  constructor(filas, cobertura = [], propias = []) {
    this.entradas = [];
    this.porGlosa = new Map();
    this.indice = new Map();
    this.indiceSinTildes = new Map();
    this.letras = new Map();
    this.maxPalabras = 1;
    const oficial = new Map(cobertura.filter((c) => c.resultado === 'encontrada').map((c) => [c.glosa, c]));
    for (const f of filas) {
      this.agregar({
        glosa: f.glosa.trim(), espanol: f.espanol.trim(),
        sinonimos: (f.sinonimos || '').split('|').map((s) => s.trim()).filter(Boolean),
        categoria: f.categoria.trim(), prioridad: parseInt(f.prioridad, 10) || 2, tipo: f.tipo.trim(), estado: f.estado.trim(),
        tituloOficial: oficial.get(f.glosa.trim())?.titulo_oficial || '', urlOficial: oficial.get(f.glosa.trim())?.url || '',
      });
    }
    for (const p of propias) this.agregarPropia(p.espanol, p.glosa);
  }

  agregar(e) {
    if (this.porGlosa.has(e.glosa)) return this.porGlosa.get(e.glosa);
    e.espanolCorto = e.espanol.split(' (')[0];
    e.terminos = [...new Set([e.espanol, e.glosa.replace(/-/g, ' '), ...e.sinonimos].map(clave).filter(Boolean))];
    this.entradas.push(e);
    this.porGlosa.set(e.glosa, e);
    if (e.tipo === 'letra') { this.letras.set(e.espanol.toLowerCase(), e.glosa); return e; }
    if (e.tipo === 'fondo') return e;
    for (const s of [e.espanol, ...e.sinonimos]) {
      const k = normalizar(s);
      if (!k || s.trim().startsWith('(')) continue;
      if (!this.indice.has(k)) this.indice.set(k, e.glosa);
      const st = sinTildes(k);
      if (!this.indiceSinTildes.has(st)) this.indiceSinTildes.set(st, e.glosa);
      this.maxPalabras = Math.max(this.maxPalabras, k.split(' ').length);
    }
    return e;
  }

  /** Glosa para una palabra que la persona añade: «Señora Pérez» → SEÑORA-PEREZ. */
  static glosaDe(espanol) {
    return sinTildes(normalizar(espanol)).toUpperCase().replace(/\s+/g, '-');
  }

  agregarPropia(espanol, glosa) {
    const limpio = (espanol || '').trim().replace(/\s+/g, ' ');
    if (!limpio) return null;
    const existente = this.buscar(normalizar(limpio));
    if (existente) return this.porGlosa.get(existente);
    const g = glosa || Vocabulario.glosaDe(limpio);
    if (!g) return null;
    return this.agregar({ glosa: g, espanol: limpio, sinonimos: [], categoria: 'propias', prioridad: 1, tipo: 'seña', estado: 'propia', propia: true, tituloOficial: '', urlOficial: '' });
  }

  existe(glosa) { const e = this.porGlosa.get(glosa); return !!e && e.tipo !== 'fondo'; }

  buscar(fraseNormalizada) {
    if (this.indice.has(fraseNormalizada)) return this.indice.get(fraseNormalizada);
    if (FUNCIONALES.has(fraseNormalizada)) return null;
    return this.indiceSinTildes.get(sinTildes(fraseNormalizada)) || null;
  }

  espanolDe(glosa) {
    if (glosa.startsWith('DACT:')) return glosa.slice(5);
    const m = NUM.exec(glosa);
    if (m) return m[1];
    const e = this.porGlosa.get(glosa);
    return e ? e.espanolCorto : glosa.toLowerCase().replace(/-/g, ' ');
  }

  deletreo(palabra) {
    const salida = [];
    for (const c of sinTildes(palabra.toLowerCase())) { const g = this.letras.get(c); if (g) salida.push(g); }
    return salida;
  }

  get categorias() {
    return [...new Set(this.entradas.filter((e) => e.tipo !== 'fondo').map((e) => e.categoria))].sort();
  }

  /** Búsqueda del diccionario: primero las que empiezan por el texto, luego las que lo contienen. */
  consultar(texto, categoria = null) {
    const q = clave(texto || '');
    const empiezan = [], contienen = [];
    for (const e of this.entradas) {
      if (e.tipo === 'fondo' || (categoria && e.categoria !== categoria)) continue;
      if (!q) { empiezan.push(e); continue; }
      if (e.terminos.some((t) => t.startsWith(q))) empiezan.push(e);
      else if (e.terminos.some((t) => t.includes(q))) contienen.push(e);
    }
    if (q) {
      const alfa = (a, b) => (clave(a.espanol) < clave(b.espanol) ? -1 : clave(a.espanol) > clave(b.espanol) ? 1 : 0);
      empiezan.sort(alfa);
      contienen.sort(alfa);
    }
    return empiezan.concat(contienen);
  }

  // ---------------------------------------------------------------- español → glosas

  /** Traducción palabra por palabra: busca la frase más larga que exista en el vocabulario. */
  literal(texto) {
    const tokens = normalizar(texto).split(' ').filter(Boolean);
    const salida = [];
    let i = 0;
    while (i < tokens.length) {
      let encontrada = null, n = Math.min(this.maxPalabras, tokens.length - i);
      for (; n > 0; n--) { encontrada = this.buscar(tokens.slice(i, i + n).join(' ')); if (encontrada) break; }
      if (encontrada) { salida.push(encontrada); i += n; continue; }
      const tok = tokens[i++];
      if (/^\d+$/.test(tok)) salida.push(...(Number(tok) <= 10 && tok.length <= 2 ? ['NUM-' + Number(tok)] : [...tok].map((c) => 'NUM-' + c)));
      else if (!FUNCIONALES.has(tok)) salida.push('DACT:' + tok);
    }
    return salida;
  }

  /** Pasos para el avatar: una seña, o el deletreo de una palabra que no está en el vocabulario. */
  plan(glosas) {
    const plan = [];
    for (const g0 of glosas) {
      const g = g0.trim();
      if (g.toUpperCase().startsWith('DACT:')) {
        const palabra = g.slice(5).trim().toLowerCase();
        plan.push({ deletreo: true, palabra, letras: this.deletreo(palabra) });
        continue;
      }
      const m = NUM.exec(g);
      if (m && !this.existe(g)) { for (const c of m[1]) plan.push({ glosa: 'NUM-' + c, espanol: c }); continue; }
      if (this.existe(g)) {
        const e = this.porGlosa.get(g);
        if (e.tipo === 'letra') plan.push({ deletreo: true, palabra: e.espanol, letras: [g] });
        else plan.push({ glosa: g, espanol: this.espanolDe(g) });
        continue;
      }
      const palabra = g.replace(/-/g, ' ').toLowerCase();
      plan.push({ deletreo: true, palabra, letras: this.deletreo(palabra) });
    }
    return plan;
  }

  /** @returns {{ texto, glosas, plan, pregunta }} */
  textoAGlosas(texto) {
    const t = (texto || '').trim();
    const glosas = this.literal(t);
    return { texto: t, glosas, plan: this.plan(glosas), pregunta: /[?¿]/.test(t) };
  }

  // ---------------------------------------------------------------- glosas → español

  glosasATexto(glosas) {
    const limpias = glosas.map((g) => g.trim()).filter((g) => g && g !== '_FONDO');
    if (limpias.length === 0) return '';
    let texto = limpias.map((g) => this.espanolDe(g)).join(' ').trim();
    if (!texto) return '';
    texto = texto[0].toUpperCase() + texto.slice(1);
    return limpias.some((g) => PREGUNTAS.has(g)) ? `¿${texto}?` : `${texto}.`;
  }
}

export function leerFrases(csv) {
  return leerCsv(csv).map((f) => ({
    id: f.id, espanol: f.espanol.trim(), direccion: f.direccion.trim(), rapida: f.rapida.trim() === '1',
    glosas: (f.glosas || '').split(/\s+/).filter(Boolean), estado: f.estado.trim(), contexto: (f.contexto || '').trim(),
  }));
}

export const NOMBRES_CATEGORIA = { tramites: 'Trámites', comunicacion: 'Comunicación', cortesia: 'Cortesía', dactilologico: 'Alfabeto manual', numeros: 'Números', propias: 'Mis señas' };
export function nombreCategoria(c) {
  return NOMBRES_CATEGORIA[c] || (c ? c[0].toUpperCase() + c.slice(1) : '');
}
