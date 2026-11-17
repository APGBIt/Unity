// Ayudas mínimas para construir la interfaz sin bibliotecas.
export function h(etiqueta, props, ...hijos) {
  const el = document.createElement(etiqueta);
  if (props) {
    for (const [k, v] of Object.entries(props)) {
      if (v === null || v === undefined || v === false) continue;
      if (k === 'class') el.className = v;
      else if (k === 'text') el.textContent = v;
      else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2).toLowerCase(), v);
      else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
      else if (k in el && k !== 'list' && typeof v !== 'string') el[k] = v;
      else el.setAttribute(k, v === true ? '' : v);
    }
  }
  agregar(el, hijos);
  return el;
}

export function agregar(el, hijos) {
  for (const c of hijos.flat(Infinity)) {
    if (c === null || c === undefined || c === false) continue;
    el.appendChild(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return el;
}

export function vaciar(el) { while (el.firstChild) el.removeChild(el.firstChild); return el; }
export function poner(el, ...hijos) { return agregar(vaciar(el), hijos); }

export const boton = (texto, alTocar, clase = '', props = {}) => h('button', { type: 'button', class: 'btn ' + clase, onclick: alTocar, ...props }, texto);

export function barra(titulo, alVolver, extra) {
  return h('header', { class: 'barra' },
    alVolver ? h('button', { type: 'button', class: 'volver', onclick: alVolver, 'aria-label': 'Volver' }, '‹') : null,
    h('h1', { text: titulo }), extra || null);
}

let nodoAviso = null, tAviso = null;
/** Mensaje breve en la parte baja de la pantalla. */
export function aviso(texto, tipo = '') {
  if (!nodoAviso) { nodoAviso = h('div', { class: 'aviso', role: 'status', 'aria-live': 'polite' }); document.body.appendChild(nodoAviso); }
  nodoAviso.textContent = texto;
  nodoAviso.className = 'aviso visible ' + tipo;
  clearTimeout(tAviso);
  tAviso = setTimeout(() => { nodoAviso.className = 'aviso'; }, 3600);
}

export function ocultarAviso() { clearTimeout(tAviso); if (nodoAviso) nodoAviso.className = 'aviso'; }

/** Ventana de confirmación propia (no usa los diálogos del navegador). Devuelve una promesa con true/false. */
export function confirmar({ titulo, texto, aceptar = 'Aceptar', cancelar = 'Cancelar', peligro = false, contenido = null }) {
  return new Promise((ok) => {
    const cerrar = (v) => { velo.remove(); ok(v); };
    const velo = h('div', { class: 'velo', role: 'dialog', 'aria-modal': 'true' },
      h('div', { class: 'caja' },
        h('h2', { text: titulo }), texto ? h('p', { text: texto }) : null, contenido,
        h('div', { class: 'fila' },
          cancelar ? boton(cancelar, () => cerrar(false), 'suave') : null,
          boton(aceptar, () => cerrar(true), peligro ? 'peligro' : 'primario'))));
    document.body.appendChild(velo);
  });
}

export function vibrar(ms = 40) { try { if (navigator.vibrate) navigator.vibrate(ms); } catch { /* */ } }

export function descargar(nombre, texto, tipo = 'application/json') {
  const url = URL.createObjectURL(new Blob([texto], { type: tipo }));
  const a = h('a', { href: url, download: nombre });
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}
