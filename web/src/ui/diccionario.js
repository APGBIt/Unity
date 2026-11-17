import { h, barra, boton, poner } from './dom.js';
import { app, clipDe, conteo, MIN_EJEMPLOS } from '../app/estado.js';
import { nombreCategoria } from '../nucleo/vocabulario.js';
import { clave } from '../nucleo/texto.js';
import { Avatar } from '../app/dibujo.js';

const MAXIMO = 80;

export function pantallaDiccionario(raiz, ir, volver, glosaInicial) {
  let categoria = '';
  const campo = h('input', { type: 'search', class: 'campo', placeholder: 'Buscar una palabra…', 'aria-label': 'Buscar', autocomplete: 'off', oninput: () => pintar() });
  const temas = h('select', { class: 'campo', 'aria-label': 'Tema', onchange: (e) => { categoria = e.target.value; pintar(); } },
    h('option', { value: '', text: 'Todos los temas' }), app.vocab.categorias.map((c) => h('option', { value: c, text: nombreCategoria(c) })));
  const cuenta = h('p', { class: 'nota' });
  const lista = h('div', { class: 'lista' });
  const ficha = h('div', { class: 'ficha', hidden: true });
  let avatar = null;

  function abrir(e) {
    const clip = clipDe(e.glosa), n = conteo(e.glosa);
    const lienzo = h('canvas', { class: 'lienzo-avatar' });
    const validada = e.estado === 'validada';
    const propia = clave(e.espanol);
    const otras = e.sinonimos.filter((x, i, l) => clave(x) !== propia && l.findIndex((y) => clave(y) === clave(x)) === i);
    poner(ficha,
      barra(e.espanol, () => { if (avatar) avatar.detener(); ficha.hidden = true; }),
      h('main', { class: 'pagina' },
        h('p', { class: 'dato', text: `Glosa: ${e.glosa} · ${nombreCategoria(e.categoria)}` }),
        otras.length ? h('p', { class: 'dato', text: 'También: ' + otras.join(', ') }) : null,
        h('p', { class: 'etiqueta ' + (validada ? 'ok' : e.propia ? '' : 'pendiente'), text: e.propia ? 'Seña añadida en este teléfono.' : validada ? 'Validada por la comunidad sorda.' : 'Propuesta: falta validarla con personas sordas e intérpretes.' }),
        e.tituloOficial ? h('p', { class: 'dato' }, `En el Diccionario Oficial: «${e.tituloOficial}». `, e.urlOficial ? h('a', { href: e.urlOficial, target: '_blank', rel: 'noopener', text: 'Abrir la entrada' }) : null)
          : e.propia ? null : h('p', { class: 'dato', text: 'No se localizó en el índice del Diccionario Oficial.' }),
        h('div', { class: 'marco-avatar' }, lienzo, clip ? null : h('p', { text: 'Todavía no hay una grabación de esta seña en este teléfono.' })),
        h('p', { class: 'nota', text: clip ? `Así la enseñaron en este teléfono (${n} ${n === 1 ? 'ejemplo' : 'ejemplos'}${n < MIN_EJEMPLOS ? `; faltan ${MIN_EJEMPLOS - n} para reconocerla` : ''}). No es una referencia oficial.` : '' }),
        h('div', { class: 'fila' }, clip ? boton('Ver otra vez', () => avatar.reproducir(clip.sec, clip.duracionMs), 'suave') : null,
          boton(n ? 'Enseñar más ejemplos' : 'Enseñar esta seña', () => ir('ensenar/' + encodeURIComponent(e.glosa)), 'primario'))));
    ficha.hidden = false;
    avatar = new Avatar(lienzo);
    avatar.velocidad = app.ajustes.velocidadAvatar;
    if (clip) requestAnimationFrame(() => avatar.reproducir(clip.sec, clip.duracionMs));
  }

  function pintar() {
    const r = app.vocab.consultar(campo.value, categoria || null);
    const visibles = r.slice(0, MAXIMO);
    cuenta.textContent = r.length === 0 ? 'No está en el vocabulario. Puede añadirla en «Enseñar señas» o deletrearla con el alfabeto manual.'
      : visibles.length < r.length ? `Mostrando ${visibles.length} de ${r.length}. Escriba para acotar.` : `${r.length} ${r.length === 1 ? 'seña' : 'señas'}`;
    poner(lista, visibles.map((e) => h('button', { type: 'button', class: 'renglon', onclick: () => abrir(e) },
      h('span', { text: e.espanol }), conteo(e.glosa) ? h('span', { class: 'marca-clip', title: 'Tiene grabación', text: '▶' }) : null)));
  }

  poner(raiz, barra('Diccionario', volver), h('main', { class: 'pagina' }, campo, temas, cuenta, lista), ficha);
  pintar();
  if (glosaInicial && app.vocab.existe(glosaInicial)) abrir(app.vocab.porGlosa.get(glosaInicial));
  return () => { if (avatar) avatar.detener(); };
}
