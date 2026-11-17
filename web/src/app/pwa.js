// Instalación como app y funcionamiento sin conexión.
let diferido = null;
let instalada = false;
const oyentes = new Set();
const avisar = () => oyentes.forEach((f) => f());

export const enIOS = () => /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
export const comoApp = () => window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;

export function iniciarPWA() {
  const seguro = location.protocol === 'https:' || location.hostname === 'localhost' || location.hostname === '127.0.0.1';
  if ('serviceWorker' in navigator && seguro) window.addEventListener('load', () => { navigator.serviceWorker.register('sw.js').catch(() => {}); });
  window.addEventListener('beforeinstallprompt', (e) => { e.preventDefault(); diferido = e; avisar(); });
  window.addEventListener('appinstalled', () => { instalada = true; diferido = null; avisar(); });
}

export const estadoInstalacion = () => ({ puedePedir: !!diferido, instalada: instalada || comoApp(), ios: enIOS() });
export const alCambiarInstalacion = (f) => { oyentes.add(f); return () => oyentes.delete(f); };

export async function pedirInstalacion() {
  if (!diferido) return false;
  diferido.prompt();
  try { await diferido.userChoice; } catch { /* */ }
  diferido = null;
  avisar();
  return true;
}
