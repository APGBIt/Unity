// Estado compartido de la app: vocabulario, señas enseñadas, reconocedor y ajustes.
import csvVocabulario from '../../../vocabulario/vocabulario_mvp.csv';
import csvCobertura from '../../../vocabulario/cobertura_diccionario_oficial.csv';
import csvFrases from '../../../vocabulario/frases_mvp.csv';
import csvEmergencias from '../../../vocabulario/emergencias.csv';
import { leerCsv } from '../nucleo/texto.js';
import { Vocabulario, leerFrases } from '../nucleo/vocabulario.js';
import { ReconocedorPorEjemplos } from '../nucleo/reconocedor.js';
import * as Paquete from '../nucleo/paquete.js';
import { Almacen } from './almacen.js';

export const MIN_EJEMPLOS = 3;   // con menos, la seña todavía no se usa para reconocer
export const META_EJEMPLOS = 5;  // lo recomendado
const SENSIBILIDAD = { estricta: 2.1, normal: 2.4, tolerante: 2.8 };

export const app = {
  almacen: new Almacen(),
  ajustes: null,
  vocab: null,
  frases: leerFrases(csvFrases),
  emergencias: leerFrases(csvEmergencias),
  ejemplos: [],      // enseñados en este teléfono
  ejemplosBase: [],  // incluidos con la app (paquetes/base.json)
  baseInfo: null,
  propias: [],
  reconocedor: new ReconocedorPorEjemplos({ minEjemplos: MIN_EJEMPLOS }),
  oyentes: new Set(),
};

const avisar = () => app.oyentes.forEach((f) => { try { f(); } catch { /* */ } });
const nuevoId = () => 'ej-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 8);

function construirVocabulario() {
  app.vocab = new Vocabulario(leerCsv(csvVocabulario), leerCsv(csvCobertura), app.propias);
}

/** Todos los ejemplos en uso: los incluidos con la app (si están activados) y los de este teléfono. */
export const todos = () => (app.ajustes && app.ajustes.usarBase !== false ? app.ejemplosBase.concat(app.ejemplos) : app.ejemplos);

/**
 * Señas incluidas con la app: un paquete publicado junto a ella. No se guardan en el
 * teléfono ni se exportan; se suman a las que la persona enseña.
 */
export async function cargarBase(obtener = (u) => fetch(u, { cache: 'no-cache' })) {
  try {
    const r = await obtener('paquetes/base.json');
    if (!r || !r.ok) return 0;
    const p = Paquete.importar(await r.json());
    for (const x of p.propias) agregarPropia(x.espanol, x.glosa, false);
    app.ejemplosBase = p.ejemplos.filter((e) => app.vocab.existe(e.glosa) || (e.espanol && agregarPropia(e.espanol, e.glosa, false)))
      .map((e, i) => ({ id: 'base-' + i, glosa: e.glosa, espanol: app.vocab.espanolDe(e.glosa), sec: e.sec, creado: e.creado, duracionMs: e.duracionMs || 1000, base: true }));
    app.baseInfo = app.ejemplosBase.length === 0 ? null : { autor: p.autor, senas: new Set(app.ejemplosBase.map((e) => e.glosa)).size, ejemplos: app.ejemplosBase.length };
    reentrenar();
    return app.ejemplosBase.length;
  } catch { return 0; }
}

export function reentrenar() {
  app.reconocedor = new ReconocedorPorEjemplos({
    minEjemplos: MIN_EJEMPLOS, aceptarEspejo: app.ajustes.otraMano,
    factorDesconocida: SENSIBILIDAD[app.ajustes.sensibilidad] || SENSIBILIDAD.normal,
  });
  app.reconocedor.fijar(todos());
  avisar();
}

export async function iniciar() {
  await app.almacen.abrir();
  app.ajustes = app.almacen.leerAjustes();
  app.propias = app.almacen.leerPropias();
  construirVocabulario();
  app.ejemplos = (await app.almacen.ejemplos()).filter((e) => e && e.glosa && e.sec && e.sec.length);
  // Una seña propia cuyo registro se perdió se recupera a partir de sus ejemplos.
  for (const e of app.ejemplos) if (!app.vocab.existe(e.glosa) && e.espanol) agregarPropia(e.espanol, e.glosa, false);
  reentrenar();
  await cargarBase();
}

export function guardarAjustes(cambios) {
  Object.assign(app.ajustes, cambios);
  app.almacen.guardarAjustes(app.ajustes);
  if ('otraMano' in cambios || 'sensibilidad' in cambios || 'usarBase' in cambios) reentrenar(); else avisar();
}

export const conteo = (glosa) => todos().reduce((n, e) => n + (e.glosa === glosa ? 1 : 0), 0);
export const ejemplosDe = (glosa) => todos().filter((e) => e.glosa === glosa);
export const lista = (glosa) => conteo(glosa) >= MIN_EJEMPLOS;
export const senasListas = () => { const c = new Map(); for (const e of todos()) c.set(e.glosa, (c.get(e.glosa) || 0) + 1); return [...c.values()].filter((n) => n >= MIN_EJEMPLOS).length; };
export const senasEmpezadas = () => new Set(todos().map((e) => e.glosa)).size;

/** El ejemplo que el avatar reproduce para una seña: el más reciente. */
export function clipDe(glosa) {
  const es = ejemplosDe(glosa);
  return es.length ? es[es.length - 1] : null;
}

export async function agregarEjemplo(glosa, sec, duracionMs) {
  const e = { id: nuevoId(), glosa, espanol: app.vocab.espanolDe(glosa), sec, creado: new Date().toISOString(), duracionMs };
  app.ejemplos.push(e);
  const guardado = await app.almacen.guardar(e);
  reentrenar();
  return { ejemplo: e, guardado };
}

export async function borrarEjemplo(id) {
  app.ejemplos = app.ejemplos.filter((e) => e.id !== id);
  await app.almacen.borrar(id);
  reentrenar();
}

export async function borrarSena(glosa) {
  for (const e of ejemplosDe(glosa)) if (!e.base) await app.almacen.borrar(e.id);
  app.ejemplos = app.ejemplos.filter((e) => e.glosa !== glosa);
  const antes = app.propias.length;
  app.propias = app.propias.filter((p) => p.glosa !== glosa);
  if (app.propias.length !== antes) { app.almacen.guardarPropias(app.propias); construirVocabulario(); }
  reentrenar();
}

export function agregarPropia(espanol, glosa, notificar = true) {
  const e = app.vocab.agregarPropia(espanol, glosa);
  if (e && e.propia && !app.propias.some((p) => p.glosa === e.glosa)) {
    app.propias.push({ glosa: e.glosa, espanol: e.espanol });
    app.almacen.guardarPropias(app.propias);
  }
  if (notificar) avisar();
  return e;
}

export function exportarPaquete(consentimiento) {
  return Paquete.exportar(app.ejemplos, app.propias, { autor: app.ajustes.autor, consentimiento });
}

/** Suma al teléfono las señas de un paquete. Devuelve cuántos ejemplos entraron. */
export async function importarPaquete(obj) {
  const r = Paquete.importar(obj);
  for (const p of r.propias) agregarPropia(p.espanol, p.glosa, false);
  const ids = new Set(app.ejemplos.map((e) => e.id));
  let nuevos = 0;
  for (const e of r.ejemplos) {
    if (!app.vocab.existe(e.glosa)) { if (e.espanol) agregarPropia(e.espanol, e.glosa, false); else continue; }
    if (!app.vocab.existe(e.glosa)) continue;
    if (!e.id || ids.has(e.id)) { if (e.id && ids.has(e.id)) continue; e.id = nuevoId(); }
    ids.add(e.id);
    const ej = { id: e.id, glosa: e.glosa, espanol: app.vocab.espanolDe(e.glosa), sec: e.sec, creado: e.creado || new Date().toISOString(), duracionMs: e.duracionMs || 1000 };
    app.ejemplos.push(ej);
    await app.almacen.guardar(ej);
    nuevos++;
  }
  reentrenar();
  return { nuevos, errores: r.errores, autor: r.autor };
}

export async function borrarTodo() {
  await app.almacen.borrarTodo();
  app.ejemplos = [];
  app.propias = [];
  construirVocabulario();
  reentrenar();
  await cargarBase();
}
