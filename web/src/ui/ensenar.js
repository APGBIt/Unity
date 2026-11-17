import { h, aviso, barra, boton, confirmar, poner, vibrar } from './dom.js';
import { crearCamara } from './camara.js';
import { extraerDeVideo, MAX_VIDEO_S } from '../app/detector.js';
import { cortarSenas } from '../nucleo/cortes.js';
import { META_EJEMPLOS, MIN_EJEMPLOS, agregarEjemplo, agregarPropia, app, borrarEjemplo, borrarSena, conteo, ejemplosDe, senasListas } from '../app/estado.js';
import { nombreCategoria } from '../nucleo/vocabulario.js';
import { remuestrear } from '../nucleo/remuestreo.js';
import { IDX_MUNECA_DER, IDX_MUNECA_IZQ } from '../nucleo/formato.g.js';
import { presente } from '../nucleo/segmentacion.js';
import { Avatar } from '../app/dibujo.js';

const MAXIMO = 120;

/** Lista de señas con su avance. */
export function pantallaEnsenar(raiz, ir, volver) {
  let filtro = 'todas', categoria = '';
  const campo = h('input', { type: 'search', class: 'campo', placeholder: 'Buscar una palabra…', 'aria-label': 'Buscar', autocomplete: 'off', oninput: () => pintar() });
  const temas = h('select', { class: 'campo', 'aria-label': 'Tema', onchange: (e) => { categoria = e.target.value; pintar(); } });
  const filtros = h('div', { class: 'pestanas' });
  const cuenta = h('p', { class: 'nota' });
  const lista = h('div', { class: 'lista' });

  async function anadir() {
    const entrada = h('input', { type: 'text', class: 'campo', placeholder: 'Por ejemplo: un nombre, un lugar…', maxlength: 60, 'aria-label': 'Palabra nueva' });
    const ok = await confirmar({ titulo: 'Añadir una palabra', texto: 'Escriba la palabra o el nombre en español. Después le enseña su seña.', aceptar: 'Añadir', contenido: entrada });
    if (!ok) return;
    const e = agregarPropia(entrada.value);
    if (!e) { aviso('Escriba una palabra.'); return; }
    ir('ensenar/' + encodeURIComponent(e.glosa));
  }

  function pintar() {
    poner(temas, h('option', { value: '', text: 'Todos los temas' }), app.vocab.categorias.map((c) => h('option', { value: c, text: nombreCategoria(c), selected: c === categoria })));
    poner(filtros, [['todas', 'Todas'], ['listas', 'Listas'], ['pendientes', 'Pendientes']].map(([k, t]) => boton(t, () => { filtro = k; pintar(); }, 'pestana' + (filtro === k ? ' activa' : ''))));
    let r = app.vocab.consultar(campo.value, categoria || null);
    if (filtro === 'listas') r = r.filter((e) => conteo(e.glosa) >= MIN_EJEMPLOS);
    if (filtro === 'pendientes') r = r.filter((e) => conteo(e.glosa) < MIN_EJEMPLOS);
    const visibles = r.slice(0, MAXIMO);
    const listas = senasListas();
    cuenta.textContent = `${listas} ${listas === 1 ? 'seña lista' : 'señas listas'}. ` + (visibles.length < r.length ? `Mostrando ${visibles.length} de ${r.length}; escriba para acotar.` : `${r.length} en esta lista.`);
    poner(lista, visibles.map((e) => {
      const n = conteo(e.glosa);
      return h('button', { type: 'button', class: 'renglon', 'data-glosa': e.glosa, onclick: () => ir('ensenar/' + encodeURIComponent(e.glosa)) },
        h('span', { text: e.espanol }),
        h('span', { class: 'avance' + (n >= MIN_EJEMPLOS ? ' lista' : n ? ' empezada' : ''), text: n >= MIN_EJEMPLOS ? `✓ ${n}` : n ? `${n} de ${MIN_EJEMPLOS}` : 'sin enseñar' }));
    }));
  }

  poner(raiz, barra('Enseñar señas', volver),
    h('main', { class: 'pagina' },
      h('p', { class: 'intro', text: `Elija una palabra y haga su seña frente a la cámara ${MIN_EJEMPLOS} veces. Con eso la app ya la reconoce; con ${META_EJEMPLOS} acierta más.` }),
      campo, temas, filtros, cuenta, lista,
      boton('+ Añadir una palabra que no está', anadir, 'suave')));
  pintar();
  return () => {};
}

/** Grabación de ejemplos de una seña. */
export function pantallaEnsenarSena(raiz, ir, volver, glosa) {
  const e = app.vocab.porGlosa.get(glosa);
  if (!e || e.tipo === 'fondo') { ir('ensenar'); return () => {}; }
  let grabando = false, vivo = true;
  const estado = h('p', { class: 'estado-grabacion', role: 'status', 'aria-live': 'polite' });
  const contador = h('div', { class: 'contador' });
  const tomas = h('div', { class: 'tomas' });
  const lienzo = h('canvas', { class: 'lienzo-avatar chico' });
  const avatar = new Avatar(lienzo);
  const bGrabar = boton('', () => { grabando = !grabando; camara.reiniciar(); pintar(); }, 'primario grande');
  const resultado = h('p', { class: 'resultado-prueba' });

  function pintar(mensaje) {
    const n = conteo(glosa);
    poner(contador, h('strong', { text: String(n) }), h('span', { text: n >= MIN_EJEMPLOS ? (n >= META_EJEMPLOS ? 'ejemplos · lista' : `ejemplos · lista (mejor con ${META_EJEMPLOS})`) : `de ${MIN_EJEMPLOS} ejemplos` }));
    contador.classList.toggle('lista', n >= MIN_EJEMPLOS);
    bGrabar.textContent = grabando ? 'Terminar' : n ? 'Grabar más ejemplos' : 'Grabar ejemplos';
    bGrabar.classList.toggle('grabando', grabando);
    estado.textContent = mensaje || (grabando ? 'Grabando. Con las manos abajo, haga la seña una vez y vuelva a bajarlas.'
      : n >= MIN_EJEMPLOS ? 'Pruébela: haga la seña y mire si la reconoce.' : 'Toque «Grabar ejemplos» y haga la seña.');
    poner(tomas, ejemplosDe(glosa).map((ej, i) => h('span', { class: 'toma' + (ej.base ? ' incluida' : '') },
      h('button', { type: 'button', 'aria-label': `Ver el ejemplo ${i + 1}`, title: ej.base ? 'Incluido con la app' : '', onclick: () => avatar.reproducir(ej.sec, ej.duracionMs) }, `▶ ${i + 1}`),
      ej.base ? null : h('button', { type: 'button', class: 'quitar', 'aria-label': `Borrar el ejemplo ${i + 1}`, onclick: async () => { await borrarEjemplo(ej.id); avatar.limpiar(); pintar('Ejemplo borrado.'); } }, '✕'))));
  }

  const camara = crearCamara({
    alSena: async (s) => {
      if (!vivo) return;
      const conManos = s.cuadros.filter((c) => presente(c, IDX_MUNECA_IZQ) || presente(c, IDX_MUNECA_DER)).length;
      const duracion = s.finMs - s.inicioMs;
      const sec = remuestrear(s.cuadros);
      if (!grabando) {
        if (app.reconocedor.total === 0) return;
        const c = app.reconocedor.clasificar(sec).candidatas[0];
        const bien = c && c.glosa === glosa && c.prob >= 0.6;
        resultado.className = 'resultado-prueba ' + (bien ? 'bien' : 'mal');
        resultado.textContent = !c || c.prob < 0.35 ? 'No reconoció ninguna seña.'
          : `${bien ? 'Reconoció' : c.prob >= 0.6 ? 'La confundió con' : 'Dudó; lo más parecido fue'} «${app.vocab.espanolDe(c.glosa)}» (${Math.round(c.prob * 100)} %).`;
        return;
      }
      if (conManos < s.cuadros.length * 0.5 || duracion < 300) { pintar('No se vieron bien las manos. Acérquese un poco o mejore la luz y repita.'); return; }
      const { guardado } = await agregarEjemplo(glosa, sec, duracion);
      vibrar(60);
      const n = conteo(glosa);
      avatar.reproducir(sec, duracion);
      if (n >= META_EJEMPLOS) { grabando = false; pintar(`Ejemplo ${n} guardado. ¡Seña lista!`); }
      else pintar(`Ejemplo ${n} guardado.${n === MIN_EJEMPLOS ? ' Ya la reconoce; puede grabar más o terminar.' : ' Baje las manos y repítala.'}`);
      if (!guardado) aviso('Este navegador no deja guardar: los ejemplos se perderán al cerrar la app.', 'alerta');
    },
  });

  // Enseñar desde un archivo de video: se procesa en el teléfono con el mismo detector de la cámara.
  const archivo = h('input', { type: 'file', accept: 'video/*', hidden: true, onchange: async (ev) => {
    const f = ev.target.files && ev.target.files[0];
    ev.target.value = '';
    if (!f || !vivo) return;
    grabando = false;
    camara.detener();
    bVideo.disabled = bGrabar.disabled = true;
    try {
      pintar('Leyendo el video…');
      const r = await extraerDeVideo(f, { alProgreso: (p) => { if (vivo) estado.textContent = `Leyendo el video… ${Math.round(p * 100)} %`; } });
      if (!vivo) return;
      const senas = cortarSenas(r.cuadros, r.tiempos, META_EJEMPLOS);
      if (senas.length === 0) { pintar('En ese video no se vieron las manos. Pruebe con otro donde se vea a la persona de frente, de la cintura para arriba.'); return; }
      let ultimo = null;
      for (const s of senas) ultimo = (await agregarEjemplo(glosa, s.sec, s.duracionMs)).ejemplo;
      vibrar(60);
      avatar.reproducir(ultimo.sec, ultimo.duracionMs);
      pintar(`${senas.length} ${senas.length === 1 ? 'ejemplo tomado' : 'ejemplos tomados'} del video. Mírelos con ▶ y borre los que no sean la seña.`);
    } catch (e) {
      if (vivo) pintar(e && e.message ? e.message : 'No se pudo leer el video.');
    } finally {
      bVideo.disabled = bGrabar.disabled = false;
      if (vivo) camara.iniciar();
    }
  } });
  const bVideo = boton('Aprender de un video', () => archivo.click(), 'suave');

  async function olvidar() {
    const ok = await confirmar({ titulo: `¿Borrar «${e.espanol}»?`, texto: e.propia ? 'Se borran la palabra y todos sus ejemplos.' : 'Se borran los ejemplos de esta seña grabados en este teléfono.', aceptar: 'Borrar', peligro: true });
    if (!ok) return;
    await borrarSena(glosa);
    if (e.propia) ir('ensenar'); else { avatar.limpiar(); pintar(); }
  }

  poner(raiz, barra(e.espanol, volver),
    h('main', { class: 'ensenar-sena' },
      camara.nodo,
      h('section', { class: 'panel-sordo' },
        h('div', { class: 'fila-entre' }, contador, lienzo),
        estado, bGrabar, bVideo, archivo, resultado, tomas,
        e.urlOficial ? h('a', { class: 'enlace-oficial', href: e.urlOficial, target: '_blank', rel: 'noopener', text: `Ver «${e.tituloOficial}» en el Diccionario Oficial ↗` }) : null,
        h('div', { class: 'fila' },
          boton('Ver en el diccionario', () => ir('diccionario/' + encodeURIComponent(glosa)), 'suave chico'),
          boton('Borrar', olvidar, 'suave chico rojo')),
        h('p', { class: 'nota', text: `Se guardan solo los puntos del cuerpo y las manos, en este teléfono. No se guarda video. Desde un archivo, use videos suyos o que tenga permiso para usar (hasta ${MAX_VIDEO_S} segundos).` }))));
  pintar();
  camara.iniciar();
  return () => { vivo = false; avatar.detener(); camara.detener(); };
}
