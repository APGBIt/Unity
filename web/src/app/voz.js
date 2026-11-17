// Voz: decir en voz alta (síntesis del teléfono) y escuchar (reconocimiento de voz del navegador).
const sintesis = typeof window !== 'undefined' ? window.speechSynthesis : null;
const Reconocimiento = typeof window !== 'undefined' ? (window.SpeechRecognition || window.webkitSpeechRecognition) : null;

export const puedeHablar = () => !!sintesis && typeof SpeechSynthesisUtterance !== 'undefined';
export const puedeEscuchar = () => !!Reconocimiento;

export function voces() {
  if (!puedeHablar()) return [];
  const orden = ['es-DO', 'es-US', 'es-MX', 'es-419', 'es-PR', 'es-CO', 'es-ES'];
  const rango = (v) => { const i = orden.indexOf(v.lang.replace('_', '-')); return i < 0 ? orden.length : i; };
  return sintesis.getVoices().filter((v) => /^es([-_]|$)/i.test(v.lang)).sort((a, b) => rango(a) - rango(b));
}

/** Los teléfonos solo dejan hablar después de un toque: se llama una vez al entrar a una pantalla que habla. */
export function desbloquear() {
  if (!puedeHablar()) return;
  try { const u = new SpeechSynthesisUtterance(' '); u.volume = 0; sintesis.speak(u); } catch { /* */ }
}

export function decir(texto, { velocidad = 1, voz = '' } = {}) {
  if (!puedeHablar() || !texto || !texto.trim()) return false;
  try {
    sintesis.cancel();
    const u = new SpeechSynthesisUtterance(texto);
    const lista = voces();
    const elegida = lista.find((v) => v.name === voz) || lista[0];
    if (elegida) { u.voice = elegida; u.lang = elegida.lang; } else u.lang = 'es-US';
    u.rate = Math.min(1.6, Math.max(0.6, velocidad));
    sintesis.speak(u);
    window.__senasUltimaVoz = texto; // para las pruebas automáticas
    return true;
  } catch { return false; }
}

export function callar() { try { if (sintesis) sintesis.cancel(); } catch { /* */ } }

/**
 * Escucha una frase. Devuelve un objeto con detener(). En Chrome el audio se envía
 * al servicio de voz del navegador para transcribirlo; la app no lo guarda.
 */
export function escuchar({ alParcial, alFinal, alError, alTerminar }) {
  if (!Reconocimiento) { alError && alError('Este navegador no transcribe la voz. Escriba el mensaje.'); alTerminar && alTerminar(); return { detener() {} }; }
  const r = new Reconocimiento();
  r.lang = 'es-DO';
  r.continuous = false;
  r.interimResults = true;
  r.maxAlternatives = 1;
  let final = '';
  r.onresult = (ev) => {
    let parcial = '';
    for (let i = ev.resultIndex; i < ev.results.length; i++) {
      const t = ev.results[i][0].transcript;
      if (ev.results[i].isFinal) final += t; else parcial += t;
    }
    alParcial && alParcial((final + ' ' + parcial).trim());
  };
  r.onerror = (ev) => {
    const m = { 'not-allowed': 'La app no tiene permiso para usar el micrófono.', 'service-not-allowed': 'El reconocimiento de voz está desactivado en este teléfono.', network: 'Para transcribir la voz hace falta conexión a internet.', 'no-speech': 'No se oyó nada. Intente de nuevo.', 'audio-capture': 'No se encontró un micrófono.' };
    if (ev.error !== 'aborted') alError && alError(m[ev.error] || 'No se pudo transcribir la voz.');
  };
  r.onend = () => { if (final.trim()) alFinal && alFinal(final.trim()); alTerminar && alTerminar(); };
  try { r.start(); } catch { alError && alError('No se pudo iniciar el micrófono.'); alTerminar && alTerminar(); }
  return { detener() { try { r.stop(); } catch { /* */ } } };
}
