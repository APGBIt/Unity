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
