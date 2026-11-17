// Dibujo de los puntos clave: el esqueleto sobre la cámara y el avatar que repite una seña.
import * as F from '../nucleo/formato.g.js';
import { presente } from '../nucleo/segmentacion.js';

const V = F.VALORES_POR_CUADRO;
const HUESOS_MANO = F.HUESOS.filter(([a]) => a >= F.INICIO_MANO_IZQ);
const HUESOS_CUERPO = F.HUESOS.filter(([a]) => a < F.INICIO_MANO_IZQ);

export function ajustarLienzo(lienzo) {
  const r = lienzo.getBoundingClientRect();
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr));
  if (lienzo.width !== w || lienzo.height !== h) { lienzo.width = w; lienzo.height = h; }
  return { w, h, dpr };
}

/**
 * Esqueleto sobre el video. El video se muestra recortado para llenar su caja
 * (object-fit: cover), así que los puntos se llevan a esa misma geometría.
 */
export function dibujarSobreVideo(lienzo, cuadro, aspecto, espejo, color = '#2DD4BF') {
  const { w, h, dpr } = ajustarLienzo(lienzo);
  const g = lienzo.getContext('2d');
  g.clearRect(0, 0, w, h);
  if (!cuadro) return;
  const escala = Math.max(w / aspecto, h);           // alto de la imagen en píxeles del lienzo
  const ox = (w - escala * aspecto) / 2, oy = (h - escala) / 2;
  const px = (i) => { const x = ox + cuadro[i * 3] * escala; return espejo ? w - x : x; };
  const py = (i) => oy + cuadro[i * 3 + 1] * escala;
  g.lineCap = 'round';
  g.strokeStyle = color;
  g.fillStyle = color;
  g.lineWidth = 3 * dpr;
  for (const [a, b] of F.HUESOS) {
    if (!presente(cuadro, a) || !presente(cuadro, b)) continue;
    g.globalAlpha = a < F.INICIO_MANO_IZQ ? 0.45 : 0.95;
    g.beginPath(); g.moveTo(px(a), py(a)); g.lineTo(px(b), py(b)); g.stroke();
  }
  g.globalAlpha = 1;
  for (let i = F.INICIO_MANO_IZQ; i < F.INICIO_CARA; i++) {
    if (!presente(cuadro, i)) continue;
    g.beginPath(); g.arc(px(i), py(i), 2.4 * dpr, 0, Math.PI * 2); g.fill();
  }
}

/** Caja que contiene toda la seña (para encuadrar el avatar siempre igual durante la reproducción). */
export function encuadre(sec) {
  const t = sec.length / V;
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (let i = 0; i < t; i++) {
    for (let p = 0; p < F.N_PUNTOS; p++) {
      const o = i * V + p * 3;
      if (Math.abs(sec[o]) + Math.abs(sec[o + 1]) + Math.abs(sec[o + 2]) === 0) continue;
      if (sec[o] < x0) x0 = sec[o]; if (sec[o] > x1) x1 = sec[o];
      if (sec[o + 1] < y0) y0 = sec[o + 1]; if (sec[o + 1] > y1) y1 = sec[o + 1];
    }
  }
  if (!Number.isFinite(x0)) return { x0: 0, y0: 0, x1: 1, y1: 1 };
  const mx = (x1 - x0) * 0.12 + 0.02, my = (y1 - y0) * 0.1 + 0.02;
  return { x0: x0 - mx, y0: y0 - my * 2.2, x1: x1 + mx, y1: y1 + my };
}

/** Figura esquemática de un cuadro: cabeza, torso, brazos y manos, vista de frente como se ve a quien firma. */
export function dibujarAvatar(lienzo, cuadro, caja, colores = {}) {
  const { w, h, dpr } = ajustarLienzo(lienzo);
  const g = lienzo.getContext('2d');
  g.clearRect(0, 0, w, h);
  if (!cuadro) return;
  const c = { cuerpo: '#9FB0C7', mano: '#2DD4BF', cabeza: '#C9D4E3', ...colores };
  const ancho = caja.x1 - caja.x0, alto = caja.y1 - caja.y0;
  const e = Math.min(w / ancho, h / alto);
  const ox = (w - ancho * e) / 2, oy = (h - alto * e) / 2;
  const px = (i) => ox + (cuadro[i * 3] - caja.x0) * e;
  const py = (i) => oy + (cuadro[i * 3 + 1] - caja.y0) * e;
  const hay = (i) => presente(cuadro, i);
  g.lineCap = 'round'; g.lineJoin = 'round';

  const hi = F.IDX_HOMBRO_IZQ, hd = F.IDX_HOMBRO_DER;
  let anchoHombros = 0;
  if (hay(hi) && hay(hd)) {
    anchoHombros = Math.hypot(px(hi) - px(hd), py(hi) - py(hd));
    // torso
    g.fillStyle = 'rgba(159,176,199,0.16)';
    g.beginPath();
    g.moveTo(px(hi), py(hi)); g.lineTo(px(hd), py(hd));
    g.lineTo(px(hd) + (px(hi) - px(hd)) * 0.12, py(hd) + anchoHombros * 1.25);
    g.lineTo(px(hi) - (px(hi) - px(hd)) * 0.12, py(hi) + anchoHombros * 1.25);
    g.closePath(); g.fill();
  }
  g.strokeStyle = c.cuerpo;
  g.lineWidth = Math.max(4 * dpr, anchoHombros * 0.07);
  for (const [a, b] of HUESOS_CUERPO) {
    if (!hay(a) || !hay(b)) continue;
    g.beginPath(); g.moveTo(px(a), py(a)); g.lineTo(px(b), py(b)); g.stroke();
  }
  // cabeza: centrada en la nariz, con un tamaño proporcional a los hombros
  if (hay(F.IDX_NARIZ)) {
    const r = anchoHombros > 0 ? anchoHombros * 0.3 : 26 * dpr;
    g.fillStyle = 'rgba(201,212,227,0.14)';
    g.strokeStyle = c.cabeza;
    g.lineWidth = 2.5 * dpr;
    g.beginPath(); g.arc(px(F.IDX_NARIZ), py(F.IDX_NARIZ) - r * 0.1, r, 0, Math.PI * 2); g.fill(); g.stroke();
    // boca y cejas, si el detector las vio (rasgos no manuales)
    const cara = (k) => F.INICIO_CARA + k;
    g.lineWidth = 2 * dpr;
    const trazo = (ks) => { if (!ks.every((k) => hay(cara(k)))) return; g.beginPath(); ks.forEach((k, n) => (n ? g.lineTo(px(cara(k)), py(cara(k))) : g.moveTo(px(cara(k)), py(cara(k))))); g.stroke(); };
    trazo([0, 2, 1]); trazo([0, 3, 1]);        // labios
    trazo([8, 9, 10, 11, 12]); trazo([13, 14, 15, 16, 17]); // cejas
  }
  // manos
  g.strokeStyle = c.mano; g.fillStyle = c.mano;
  g.lineWidth = Math.max(3 * dpr, anchoHombros * 0.035);
  for (const [a, b] of HUESOS_MANO) {
    if (!hay(a) || !hay(b)) continue;
    g.beginPath(); g.moveTo(px(a), py(a)); g.lineTo(px(b), py(b)); g.stroke();
  }
}

/** Reproduce secuencias de 32 cuadros en un lienzo, una tras otra. */
export class Avatar {
  constructor(lienzo) { this.lienzo = lienzo; this.velocidad = 1; this.id = 0; }

  detener() { this.id++; if (this.raf) cancelAnimationFrame(this.raf); this.raf = null; }

  limpiar() { this.detener(); const g = this.lienzo.getContext('2d'); g.clearRect(0, 0, this.lienzo.width, this.lienzo.height); }

  /** @returns Promise que termina al acabar la reproducción (o al detenerla). */
  reproducir(sec, duracionMs = 1200) {
    this.detener();
    const turno = this.id;
    const t = sec.length / V;
    const caja = encuadre(sec);
    const total = Math.min(4000, Math.max(700, duracionMs)) / Math.max(0.4, this.velocidad);
    const mezcla = new Float32Array(V);
    return new Promise((fin) => {
      const inicio = performance.now();
      const paso = (ahora) => {
        if (turno !== this.id) { fin(false); return; }
        const u = Math.min(1, (ahora - inicio) / total);
        const pos = u * (t - 1), i = Math.min(t - 2, Math.floor(pos)), f = pos - i;
        const a = sec.subarray(i * V, (i + 1) * V), b = sec.subarray((i + 1) * V, (i + 2) * V);
        for (let p = 0; p < F.N_PUNTOS; p++) {
          const o = p * 3;
          const ha = presente(a, p), hb = presente(b, p);
          for (let k = 0; k < 3; k++) mezcla[o + k] = ha && hb ? a[o + k] + (b[o + k] - a[o + k]) * f : ha ? a[o + k] : hb && f > 0.5 ? b[o + k] : 0;
        }
        dibujarAvatar(this.lienzo, mezcla, caja);
        if (u >= 1) { fin(true); return; }
        this.raf = requestAnimationFrame(paso);
      };
      this.raf = requestAnimationFrame(paso);
    });
  }

  /** Muestra quieta la postura inicial de una seña. */
  posar(sec) { this.detener(); dibujarAvatar(this.lienzo, sec.subarray(0, V), encuadre(sec)); }
}
