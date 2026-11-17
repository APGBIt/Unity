// Señas RD · caché para usar la app sin conexión. No guarda video, audio ni datos personales.
const CACHE = 'senas-rd-3ed25718bd';
const NUCLEO = ['./', './index.html', './manifest.webmanifest', './icons/icon-192.png', './icons/icon-512.png'];
self.addEventListener('install', (e) => { e.waitUntil(caches.open(CACHE).then((c) => c.addAll(NUCLEO)).then(() => self.skipWaiting())); });
self.addEventListener('activate', (e) => { e.waitUntil(caches.keys().then((ks) => Promise.all(ks.filter((k) => k !== CACHE).map((k) => caches.delete(k)))).then(() => self.clients.claim())); });
self.addEventListener('fetch', (e) => {
  const req = e.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== location.origin) return;
  if (req.mode === 'navigate') {
    e.respondWith(fetch(req).then((r) => { const cp = r.clone(); caches.open(CACHE).then((c) => c.put('./index.html', cp)); return r; }).catch(() => caches.match('./index.html')));
    return;
  }
  // Las señas incluidas pueden cambiar entre versiones: primero la red y, sin conexión, la copia guardada.
  if (url.pathname.includes('/paquetes/')) {
    e.respondWith(fetch(req).then((r) => { if (r.ok) { const cp = r.clone(); caches.open(CACHE).then((c) => c.put(req, cp)); } return r; }).catch(() => caches.match(req)));
    return;
  }
  // El detector pesa unos 25 MB: se guarda la primera vez que se usa la cámara y después se sirve desde el teléfono.
  e.respondWith(caches.match(req).then((guardado) => guardado || fetch(req).then((r) => {
    if (r.ok && r.status === 200) { const cp = r.clone(); caches.open(CACHE).then((c) => c.put(req, cp)); }
    return r;
  })));
});
