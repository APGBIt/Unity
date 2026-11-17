// Lo que la app guarda en el teléfono: ejemplos de señas (IndexedDB) y ajustes (localStorage).
// Nunca guarda video ni audio. Si el navegador no permite guardar, todo sigue funcionando en memoria.
const BD = 'senas-rd', TABLA = 'ejemplos', VERSION = 1;
const K_AJUSTES = 'senas-rd.ajustes', K_PROPIAS = 'senas-rd.propias';

export const AJUSTES_POR_DEFECTO = {
  camaraFrontal: true,
  vozActiva: true,
  decirSola: true,        // decir la frase al terminar de firmar, sin tocar nada
  velocidadVoz: 1,
  voz: '',
  pausaFraseMs: 1800,
  sensibilidad: 'normal', // estricta | normal | tolerante
  detectorPreciso: false,
  velocidadAvatar: 1,
  otraMano: true,
  autor: '',
  avisoVisto: false,
};

function abrir() {
  return new Promise((ok, mal) => {
    if (typeof indexedDB === 'undefined') { mal(new Error('sin IndexedDB')); return; }
    const p = indexedDB.open(BD, VERSION);
    p.onupgradeneeded = () => { if (!p.result.objectStoreNames.contains(TABLA)) p.result.createObjectStore(TABLA, { keyPath: 'id' }); };
    p.onsuccess = () => ok(p.result);
    p.onerror = () => mal(p.error);
    p.onblocked = () => mal(new Error('base de datos bloqueada'));
  });
}

function pedir(bd, modo, fn) {
  return new Promise((ok, mal) => {
    const tx = bd.transaction(TABLA, modo);
    const r = fn(tx.objectStore(TABLA));
    tx.oncomplete = () => ok(r && r.result);
    tx.onerror = () => mal(tx.error);
    tx.onabort = () => mal(tx.error);
  });
}

export class Almacen {
  constructor() { this.bd = null; this.memoria = new Map(); this.persistente = false; }

  async abrir() {
    try { this.bd = await abrir(); this.persistente = true; } catch { this.bd = null; this.persistente = false; }
    // Pide al navegador que no borre los datos si falta espacio (puede negarse; no es un error).
    try { if (this.persistente && navigator.storage && navigator.storage.persist) navigator.storage.persist().catch(() => {}); } catch { /* */ }
    return this.persistente;
  }

  async ejemplos() {
    if (!this.bd) return [...this.memoria.values()];
    try {
      const lista = await pedir(this.bd, 'readonly', (t) => t.getAll());
      return (lista || []).map((e) => ({ ...e, sec: e.sec instanceof Float32Array ? e.sec : new Float32Array(e.sec) }));
    } catch { return [...this.memoria.values()]; }
  }

  async guardar(ejemplo) {
    this.memoria.set(ejemplo.id, ejemplo);
    if (!this.bd) return false;
    try { await pedir(this.bd, 'readwrite', (t) => t.put(ejemplo)); return true; } catch { return false; }
  }

  async borrar(id) {
    this.memoria.delete(id);
    if (!this.bd) return;
    try { await pedir(this.bd, 'readwrite', (t) => t.delete(id)); } catch { /* */ }
  }

  async borrarTodo() {
    this.memoria.clear();
    if (this.bd) { try { await pedir(this.bd, 'readwrite', (t) => t.clear()); } catch { /* */ } }
    try { localStorage.removeItem(K_PROPIAS); } catch { /* */ }
  }

  leerAjustes() {
    try { return { ...AJUSTES_POR_DEFECTO, ...(JSON.parse(localStorage.getItem(K_AJUSTES) || '{}')) }; } catch { return { ...AJUSTES_POR_DEFECTO }; }
  }

  guardarAjustes(a) { try { localStorage.setItem(K_AJUSTES, JSON.stringify(a)); } catch { /* */ } }

  leerPropias() {
    try { const p = JSON.parse(localStorage.getItem(K_PROPIAS) || '[]'); return Array.isArray(p) ? p.filter((x) => x && x.glosa && x.espanol) : []; } catch { return []; }
  }

  guardarPropias(p) { try { localStorage.setItem(K_PROPIAS, JSON.stringify(p)); } catch { /* */ } }
}
