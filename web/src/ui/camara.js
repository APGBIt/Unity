// Vista de cámara compartida por Conversación y Enseñar: video, esqueleto encima, estado y segmentación en vivo.
import { h } from './dom.js';
import { Detector, mensajeDeCamara } from '../app/detector.js';
import { dibujarSobreVideo } from '../app/dibujo.js';
import { SegmentadorEnVivo } from '../nucleo/segmentacion.js';
import { app, guardarAjustes } from '../app/estado.js';

/**
 * @param alSena    ({ cuadros, tiempos, inicioMs, finMs }) cada vez que termina una seña
 * @param alCuadro  (info) en cada cuadro: { manos, enSena, t }
 */
export function crearCamara({ alSena, alCuadro }) {
  const video = h('video', { playsinline: true, muted: true, autoplay: true, 'aria-label': 'Cámara' });
  const lienzo = h('canvas', { class: 'esqueleto' });
  const estado = h('div', { class: 'estado-camara' });
  const chip = h('div', { class: 'chip-camara', text: 'Iniciando…' });
  const girar = h('button', { type: 'button', class: 'girar', 'aria-label': 'Cambiar de cámara', title: 'Cambiar de cámara' }, '⟲');
  const nodo = h('div', { class: 'camara' }, video, lienzo, chip, girar, estado);
  const segmentador = new SegmentadorEnVivo();
  let pausada = false, ultimoChip = '';

  const ponerChip = (texto, clase) => { const k = texto + clase; if (k === ultimoChip) return; ultimoChip = k; chip.textContent = texto; chip.className = 'chip-camara ' + clase; };

  const detector = new Detector(video, {
    alEstado: (t) => { estado.textContent = t; estado.classList.toggle('visible', !!t); },
    alCuadro: (cuadro, t, info) => {
      dibujarSobreVideo(lienzo, cuadro, info.aspecto, app.ajustes.camaraFrontal && !detector.simulado);
      if (pausada) { ponerChip('En pausa', ''); return; }
      const sena = segmentador.agregar(cuadro, t);
      const enSena = segmentador.enSena;
      if (!info.cuerpo && info.manos === 0) ponerChip('No veo a nadie', 'alerta');
      else if (enSena) ponerChip('Firmando…', 'activo');
      else if (info.manos === 0) ponerChip('Manos abajo', '');
      else ponerChip('Manos a la vista', '');
      // Primero la seña recién terminada y después el cuadro: así la pausa de fin de frase se mide desde la última seña.
      if (sena && alSena) alSena(sena);
      if (alCuadro) alCuadro({ manos: info.manos, enSena, t });
    },
  });

  async function iniciar() {
    video.classList.toggle('espejo', app.ajustes.camaraFrontal);
    try {
      await detector.iniciar({ frontal: app.ajustes.camaraFrontal, preciso: app.ajustes.detectorPreciso });
    } catch (e) {
      estado.textContent = mensajeDeCamara(e);
      estado.classList.add('visible', 'error');
      ponerChip('Sin cámara', 'alerta');
    }
  }

  girar.addEventListener('click', () => { guardarAjustes({ camaraFrontal: !app.ajustes.camaraFrontal }); segmentador.reiniciar(); iniciar(); });
  // Al volver a la app (por ejemplo, después de bloquear el teléfono) la cámara se reabre sola.
  const alVolverVisible = () => { if (document.visibilityState === 'visible' && nodo.isConnected && !detector.flujo && !detector.simulado) iniciar(); else if (document.visibilityState === 'hidden') detector.detener(); };
  document.addEventListener('visibilitychange', alVolverVisible);

  return {
    nodo, iniciar,
    pausar(v) { pausada = v; if (v) segmentador.reiniciar(); },
    reiniciar() { segmentador.reiniciar(); },
    detener() { document.removeEventListener('visibilitychange', alVolverVisible); detector.detener(); },
  };
}
