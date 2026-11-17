import { h, aviso, barra, boton, poner, vibrar } from './dom.js';
import { crearCamara } from './camara.js';
import { app, clipDe, senasListas } from '../app/estado.js';
import { ConstructorFrase } from '../nucleo/frase.js';
import { remuestrear } from '../nucleo/remuestreo.js';
import { Avatar } from '../app/dibujo.js';
import { callar, decir, desbloquear, escuchar, puedeEscuchar } from '../app/voz.js';

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

export function pantallaConversacion(raiz, ir, volver) {
  const frase = new ConstructorFrase();
  let dicha = false, abierta = -1, respondiendo = false, turno = 0, oyendo = null, textoFijo = '';
  const hablar = (t) => app.ajustes.vozActiva && decir(t, { velocidad: app.ajustes.velocidadVoz, voz: app.ajustes.voz });

  // ------------------------------------------------ lo que firma la persona sorda
  const texto = h('p', { class: 'traduccion vacia', text: 'Firme frente a la cámara y baje las manos entre una seña y otra.' });
  const chips = h('div', { class: 'chips' });
  const opciones = h('div', { class: 'opciones', hidden: true });
  const nota = h('p', { class: 'nota-frase' });
  const bDecir = boton('Decir', () => { dicha = true; decirFrase(true); }, 'primario');
  const bDeshacer = boton('Deshacer', () => { frase.deshacer(); abierta = -1; dicha = false; pintar(); }, 'suave');
  const bBorrar = boton('Borrar', () => { frase.limpiar(); textoFijo = ''; abierta = -1; dicha = false; callar(); pintar(); }, 'suave');

  const textoActual = () => textoFijo || app.vocab.glosasATexto(frase.glosas);

  function decirFrase(aMano) {
    const t = textoActual();
    if (!t) return;
    if (!hablar(t) && aMano) aviso(app.ajustes.vozActiva ? 'Este navegador no puede hablar. Muestre el texto.' : 'La voz está apagada en Ajustes.');
  }

  function pintar() {
    const t = textoActual();
    texto.textContent = t || (senasListas() ? 'Firme frente a la cámara y baje las manos entre una seña y otra.' : 'Aquí aparece lo que firma la persona sorda.');
    texto.classList.toggle('vacia', !t);
    poner(chips, frase.senas.map((s, i) => h('button', {
      type: 'button', class: 'chip' + (s.dudosa ? ' dudosa' : '') + (i === abierta ? ' abierta' : ''), 'data-glosa': s.glosa,
      onclick: () => { abierta = abierta === i ? -1 : i; pintar(); },
    }, app.vocab.espanolDe(s.glosa) + (s.dudosa ? ' ?' : ''), h('small', { text: s.corregida ? 'corregida' : Math.round(s.prob * 100) + ' %' }))));
    const s = frase.senas[abierta];
    opciones.hidden = !s;
    if (s) {
      poner(opciones, h('span', { text: s.alternativas.length ? '¿Era otra?' : 'No hay otra seña parecida.' }),
        s.alternativas.map((a) => boton(app.vocab.espanolDe(a.glosa), () => { frase.corregir(abierta, a.glosa); abierta = -1; pintar(); }, 'suave chico')),
        boton('Quitar', () => { frase.quitar(abierta); abierta = -1; pintar(); }, 'suave chico rojo'));
    }
    nota.textContent = frase.vacia ? '' : frase.tieneDudas ? 'Hay señas dudosas (con «?»). Tóquelas para corregirlas antes de decir la frase.'
      : 'Traducción palabra por palabra de las señas enseñadas en este teléfono.';
    for (const b of [bDecir, bDeshacer, bBorrar]) b.disabled = !t;
    bDeshacer.disabled = frase.vacia;
  }

  const camara = crearCamara({
    alSena: (s) => {
      if (respondiendo) return;
      if (app.reconocedor.total === 0) return;
      const r = app.reconocedor.clasificar(remuestrear(s.cuadros));
      const mejor = r.candidatas[0];
      // Una seña que no se reconoce no cambia nada de lo que ya está en pantalla.
      if (!mejor || mejor.prob < frase.umbralMinimo) { aviso('No reconocí esa seña. Repítala o enséñela en «Enseñar señas».'); return; }
      if (dicha || textoFijo) { frase.limpiar(); textoFijo = ''; dicha = false; abierta = -1; }
      frase.agregar(r, s.inicioMs, s.finMs);
      vibrar(25);
      pintar();
    },
    alCuadro: ({ enSena, t }) => {
      if (respondiendo || dicha || frase.vacia || enSena) return;
      if (t - frase.finMs < app.ajustes.pausaFraseMs) return;
      dicha = true; // la frase terminó: la próxima seña empieza una nueva
      if (!frase.tieneDudas && app.ajustes.decirSola) decirFrase(false);
      pintar();
    },
  });

  // ------------------------------------------------ lo que responde quien atiende
  const campo = h('input', { type: 'text', class: 'campo', placeholder: 'Escriba la respuesta…', 'aria-label': 'Respuesta', enterkeyhint: 'send', autocomplete: 'off',
    onkeydown: (e) => { if (e.key === 'Enter') enviar(); } });
  const bMic = puedeEscuchar() ? boton('🎤', alternarMic, 'suave icono', { 'aria-label': 'Hablar', title: 'Hablar' }) : null;
  const bEnviar = boton('Mostrar', enviar, 'primario');
  const panelRespuesta = h('div', { class: 'respuesta', hidden: true });
  const modalFrases = h('div', { class: 'velo', hidden: true });
  let avatar = null;

  function enviar() {
    const t = campo.value.trim();
    if (!t) { campo.focus(); return; }
    campo.value = '';
    campo.blur();
    responder(t);
  }

  function alternarMic() {
    if (oyendo) { oyendo.detener(); return; }
    callar();
    bMic.classList.add('grabando');
    campo.placeholder = 'Escuchando…';
    oyendo = escuchar({
      alParcial: (t) => { campo.value = t; },
      alFinal: (t) => { campo.value = ''; responder(t); },
      alError: (m) => aviso(m),
      alTerminar: () => { oyendo = null; bMic.classList.remove('grabando'); campo.placeholder = 'Escriba la respuesta…'; },
    });
  }

  async function responder(t) {
    const r = app.vocab.textoAGlosas(t);
    const miTurno = ++turno;
    respondiendo = true;
    camara.pausar(true);
    const lienzo = h('canvas', { class: 'lienzo-avatar' });
    const tarjeta = h('div', { class: 'tarjeta-palabra', hidden: true });
    const rotulo = h('p', { class: 'rotulo-avatar' });
    const pasos = h('div', { class: 'chips' }, r.plan.map((p, i) => h('span', { class: 'chip paso' + (tieneClip(p) ? '' : ' sin-clip'), 'data-i': i, text: p.deletreo ? p.palabra : p.espanol })));
    const conSenas = r.plan.filter(tieneClip).length;
    poner(panelRespuesta,
      h('p', { class: 'quien', text: 'Para la persona sorda' }),
      h('p', { class: 'frase-respuesta', text: r.texto }),
      h('div', { class: 'marco-avatar' }, lienzo, tarjeta),
      rotulo, pasos,
      h('p', { class: 'nota', text: r.plan.length === 0 ? '' : conSenas === 0 ? 'Ninguna de estas palabras tiene una seña enseñada todavía: se muestran escritas.'
        : conSenas < r.plan.length ? 'Las palabras sin seña enseñada se muestran escritas.' : 'Señas enseñadas en este teléfono, en el orden de la frase en español.' }),
      h('div', { class: 'fila' }, boton('Repetir', () => reproducir(), 'suave'), boton('Cerrar', cerrarRespuesta, 'primario')));
    panelRespuesta.hidden = false;
    avatar = new Avatar(lienzo);

    async function reproducir() {
      const mio = ++turno;
      avatar.velocidad = app.ajustes.velocidadAvatar;
      const marcar = (i) => pasos.querySelectorAll('.paso').forEach((n) => n.classList.toggle('actual', Number(n.dataset.i) === i));
      const escrita = async (palabra, ms) => { avatar.limpiar(); tarjeta.textContent = palabra; tarjeta.hidden = false; await espera(ms / avatar.velocidad); tarjeta.hidden = true; };
      for (let i = 0; i < r.plan.length; i++) {
        if (mio !== turno) return;
        const p = r.plan[i];
        marcar(i);
        rotulo.textContent = p.deletreo ? p.palabra : p.espanol;
        if (!p.deletreo) {
          const clip = clipDe(p.glosa);
          if (clip) await avatar.reproducir(clip.sec, clip.duracionMs); else await escrita(p.espanol, 1100);
        } else if (p.letras.length && p.letras.every((l) => clipDe(l))) {
          for (const l of p.letras) { if (mio !== turno) return; const c = clipDe(l); rotulo.textContent = `${p.palabra} · ${app.vocab.espanolDe(l).toUpperCase()}`; await avatar.reproducir(c.sec, Math.min(700, c.duracionMs)); }
        } else await escrita(p.palabra, 1300);
        if (mio !== turno) return;
        await espera(120);
      }
      if (mio === turno) { marcar(-1); rotulo.textContent = ''; }
    }
    if (miTurno === turno) reproducir();
  }
  const tieneClip = (p) => (p.deletreo ? p.letras.length > 0 && p.letras.every((l) => clipDe(l)) : !!clipDe(p.glosa));

  function cerrarRespuesta() {
    turno++;
    if (avatar) avatar.detener();
    panelRespuesta.hidden = true;
    respondiendo = false;
    camara.pausar(false);
  }

  function abrirFrases() {
    let direccion = 'oyente_a_sordo';
    const lista = h('div', { class: 'lista' });
    const pest = h('div', { class: 'pestanas' });
    const llenar = () => {
      poner(pest, boton('Dice quien atiende', () => { direccion = 'oyente_a_sordo'; llenar(); }, 'pestana' + (direccion === 'oyente_a_sordo' ? ' activa' : '')),
        boton('Dice la persona sorda', () => { direccion = 'sordo_a_oyente'; llenar(); }, 'pestana' + (direccion === 'sordo_a_oyente' ? ' activa' : '')));
      poner(lista, app.frases.filter((f) => f.rapida && f.direccion === direccion).map((f) => h('button', { type: 'button', class: 'renglon', onclick: () => {
        modalFrases.hidden = true;
        if (f.direccion === 'oyente_a_sordo') responder(f.espanol);
        else { frase.limpiar(); textoFijo = f.espanol; dicha = true; abierta = -1; pintar(); decirFrase(true); }
      } }, f.espanol)));
    };
    poner(modalFrases, h('div', { class: 'caja alta' }, h('h2', { text: 'Frases rápidas' }), pest, lista, boton('Cerrar', () => { modalFrases.hidden = true; }, 'suave')));
    llenar();
    modalFrases.hidden = false;
  }

  const sinSenas = senasListas() === 0
    ? h('div', { class: 'tarjeta fila-entre aviso-fijo' }, h('p', { text: 'Para traducir señas, primero enséñele las suyas.' }), boton('Enseñar', () => ir('ensenar'), 'primario chico'))
    : null;

  poner(raiz,
    barra('Conversación', volver, boton('Emergencia', () => ir('emergencia'), 'peligro chico')),
    h('main', { class: 'conversacion' },
      camara.nodo,
      h('section', { class: 'panel-sordo' },
        sinSenas,
        h('p', { class: 'quien', text: 'Persona sorda' }),
        texto, chips, opciones, nota,
        h('div', { class: 'fila' }, bDecir, bDeshacer, bBorrar)),
      h('section', { class: 'panel-oyente' },
        h('p', { class: 'quien', text: 'Quien atiende' }),
        h('div', { class: 'fila-campo' }, campo, bMic, bEnviar),
        boton('Frases rápidas', abrirFrases, 'suave'))),
    panelRespuesta, modalFrases);
  pintar();
  desbloquear();
  camara.iniciar();

  return () => { turno++; if (oyendo) oyendo.detener(); if (avatar) avatar.detener(); callar(); camara.detener(); };
}
