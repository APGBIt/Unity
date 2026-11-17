// Cámara + MediaPipe holístico → cuadros de puntos clave en formato LSRD-LM v1.
// Todo ocurre en el teléfono: la imagen de la cámara no se envía ni se guarda.
import { empaquetar } from '../nucleo/empaquetador.js';
import { VALORES_POR_CUADRO } from '../nucleo/formato.g.js';

const CARPETA = 'mediapipe/';
let cargaGuion = null;

function cargarGuion() {
  if (window.Holistic) return Promise.resolve();
  if (!cargaGuion) {
    cargaGuion = new Promise((ok, mal) => {
      const s = document.createElement('script');
      s.src = CARPETA + 'holistic.js';
      s.onload = () => ok();
      s.onerror = () => { cargaGuion = null; mal(new Error('No se pudo cargar el detector. Revise la conexión la primera vez que abre la cámara.')); };
      document.head.appendChild(s);
    });
  }
  return cargaGuion;
}

export function mensajeDeCamara(e) {
  const n = e && e.name;
  if (n === 'NotAllowedError' || n === 'SecurityError') return 'La app no tiene permiso para usar la cámara. Actívelo en los ajustes del navegador y vuelva a entrar.';
  if (n === 'NotFoundError' || n === 'OverconstrainedError') return 'No se encontró una cámara en este dispositivo.';
  if (n === 'NotReadableError') return 'Otra aplicación está usando la cámara. Ciérrela y vuelva a intentar.';
  return (e && e.message) || 'No se pudo abrir la cámara.';
}

const buscar = (video, t) => new Promise((ok) => {
  let listo = false;
  const fin = () => { if (!listo) { listo = true; video.removeEventListener('seeked', fin); ok(); } };
  video.addEventListener('seeked', fin);
  setTimeout(fin, 1500); // algunos teléfonos no avisan cuando el salto es muy corto
  video.currentTime = t;
});

export const MAX_VIDEO_S = 30;

/**
 * Lee un archivo de video cuadro a cuadro y devuelve sus puntos clave, como si se hubiera
 * firmado frente a la cámara. El video se procesa en el teléfono y no se guarda.
 * @returns {{ cuadros: Float32Array[], tiempos: number[], duracionS: number }}
 */
export async function extraerDeVideo(archivo, { alProgreso = () => {}, cuadrosPorSegundo = 15 } = {}) {
  await cargarGuion();
  const video = document.createElement('video');
  video.muted = true; video.playsInline = true; video.preload = 'auto';
  const url = URL.createObjectURL(archivo);
  let holistico = null;
  try {
    video.src = url;
    await new Promise((ok, mal) => {
      video.onloadeddata = ok;
      video.onerror = () => mal(new Error('Este teléfono no puede leer ese video. Pruebe con un MP4.'));
      setTimeout(() => mal(new Error('El video tardó demasiado en abrirse.')), 20000);
    });
    const duracionS = Math.min(Number.isFinite(video.duration) ? video.duration : 0, MAX_VIDEO_S);
    if (!(duracionS > 0.3) || !video.videoWidth) throw new Error('El video está vacío o es demasiado corto.');
    const aspecto = video.videoWidth / video.videoHeight;
    holistico = new window.Holistic({ locateFile: (f) => CARPETA + f });
    holistico.setOptions({ modelComplexity: 1, smoothLandmarks: true, enableSegmentation: false, refineFaceLandmarks: false, minDetectionConfidence: 0.5, minTrackingConfidence: 0.5, selfieMode: false });
    let ultimo = null;
    holistico.onResults((r) => { ultimo = r; });
    const cuadros = [], tiempos = [];
    const paso = 1 / cuadrosPorSegundo;
    for (let t = 0; t < duracionS; t += paso) {
      await buscar(video, t);
      ultimo = null;
      await holistico.send({ image: video });
      if (ultimo) { cuadros.push(empaquetar(ultimo.poseLandmarks, ultimo.leftHandLandmarks, ultimo.rightHandLandmarks, ultimo.faceLandmarks, aspecto)); tiempos.push(t * 1000); }
      alProgreso(Math.min(1, (t + paso) / duracionS));
    }
    return { cuadros, tiempos, duracionS };
  } finally {
    if (holistico) { try { await holistico.close(); } catch { /* */ } }
    URL.revokeObjectURL(url);
    video.removeAttribute('src');
    try { video.load(); } catch { /* */ }
  }
}

/**
 * Uso: const d = new Detector(video, { alCuadro, alEstado }); await d.iniciar({ frontal, preciso }); … d.detener();
 * alCuadro(cuadro: Float32Array(234), tMs, { aspecto, manos })
 */
export class Detector {
  constructor(video, { alCuadro, alEstado } = {}) {
    this.video = video;
    this.alCuadro = alCuadro || (() => {});
    this.alEstado = alEstado || (() => {});
    this.activo = false;
    this.ocupado = false;
    this.aspecto = 4 / 3;
    this.frontal = true;
    this.cuadros = 0;
  }

  get simulado() { return !!window.__senasSimulacion; }

  async iniciar({ frontal = true, preciso = false } = {}) {
    this.detener();
    this.activo = true;
    this.frontal = frontal;
    const turno = (this.turno = (this.turno || 0) + 1);
    if (this.simulado) { this.iniciarSimulacion(turno); return; }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) throw new Error('Este navegador no permite usar la cámara. Abra la app en Chrome o Safari, por https.');
    this.alEstado('Abriendo la cámara…');
    const flujo = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: { facingMode: frontal ? 'user' : 'environment', width: { ideal: 640 }, height: { ideal: 480 }, frameRate: { ideal: 30 } },
    });
    if (!this.activo || turno !== this.turno) { flujo.getTracks().forEach((t) => t.stop()); return; }
    this.flujo = flujo;
    this.video.srcObject = flujo;
    this.video.muted = true;
    this.video.playsInline = true;
    await this.video.play().catch(() => {});

    this.alEstado('Preparando el detector… (la primera vez tarda más)');
    await cargarGuion();
    if (!this.activo || turno !== this.turno) return;
    if (!this.holistico || this.preciso !== preciso) {
      if (this.holistico) { try { await this.holistico.close(); } catch { /* */ } }
      this.holistico = new window.Holistic({ locateFile: (f) => CARPETA + f });
      this.holistico.setOptions({
        modelComplexity: preciso ? 1 : 0, smoothLandmarks: true, enableSegmentation: false, refineFaceLandmarks: false,
        minDetectionConfidence: 0.5, minTrackingConfidence: 0.5, selfieMode: false,
      });
      this.preciso = preciso;
    }
    this.holistico.onResults((r) => this.recibir(r));
    this.primerResultado = false;
    const paso = async () => {
      if (!this.activo || turno !== this.turno) return;
      if (!this.ocupado && this.video.readyState >= 2 && this.video.videoWidth > 0) {
        this.ocupado = true;
        this.aspecto = this.video.videoWidth / this.video.videoHeight;
        this.tCuadro = performance.now();
        try { await this.holistico.send({ image: this.video }); } catch (e) { if (this.activo) this.alEstado('El detector falló: ' + (e && e.message ? e.message : e)); }
        this.ocupado = false;
      }
      this.rafId = requestAnimationFrame(paso);
    };
    this.rafId = requestAnimationFrame(paso);
  }

  recibir(r) {
    if (!this.activo) return;
    if (!this.primerResultado) { this.primerResultado = true; this.alEstado(''); }
    const cuadro = empaquetar(r.poseLandmarks, r.leftHandLandmarks, r.rightHandLandmarks, r.faceLandmarks, this.aspecto);
    this.cuadros++;
    this.alCuadro(cuadro, this.tCuadro, { aspecto: this.aspecto, manos: (r.leftHandLandmarks ? 1 : 0) + (r.rightHandLandmarks ? 1 : 0), cuerpo: !!r.poseLandmarks });
  }

  // Fuente simulada (pruebas automáticas): window.__senasSimulacion.siguiente() entrega cuadros ya detectados.
  iniciarSimulacion(turno) {
    const sim = window.__senasSimulacion;
    this.aspecto = sim.aspecto || 4 / 3;
    this.alEstado('');
    this.temporizador = setInterval(() => {
      if (!this.activo || turno !== this.turno) return;
      const c = sim.siguiente();
      if (!c) return;
      let manos = 0;
      for (const ini of [7, 28]) if (Math.abs(c.cuadro[ini * 3]) + Math.abs(c.cuadro[ini * 3 + 1]) > 0) manos++;
      this.cuadros++;
      this.alCuadro(c.cuadro.length === VALORES_POR_CUADRO ? c.cuadro : new Float32Array(VALORES_POR_CUADRO), c.t, { aspecto: this.aspecto, manos, cuerpo: true });
    }, 16);
  }

  detener() {
    this.activo = false;
    if (this.rafId) cancelAnimationFrame(this.rafId);
    if (this.temporizador) clearInterval(this.temporizador);
    this.rafId = this.temporizador = null;
    if (this.flujo) { this.flujo.getTracks().forEach((t) => t.stop()); this.flujo = null; }
    if (this.video) { try { this.video.srcObject = null; } catch { /* */ } }
  }
}
