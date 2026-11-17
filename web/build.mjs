// Construye la app web en ../docs: un solo HTML con todo dentro, el detector de MediaPipe
// alojado junto a la app (para que funcione sin conexión) y los archivos para instalarla.
import { build } from 'esbuild';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AQUI = dirname(fileURLToPath(import.meta.url));
const SALIDA = join(AQUI, '..', 'docs');
const DEV = process.argv.includes('--dev');
const pkg = JSON.parse(readFileSync(join(AQUI, 'package.json'), 'utf8'));

const TITULO = 'Señas RD';
const DESCRIPCION = 'Traductor de Lengua de Señas Dominicana y español con la cámara del teléfono. Todo se analiza en el dispositivo; no se guarda video.';
const TEMA = '#0B1220';

const r = await build({
  entryPoints: [join(AQUI, 'src/main.js')],
  bundle: true, minify: !DEV, format: 'iife', target: ['es2020', 'safari14', 'chrome90'],
  write: false, legalComments: 'none', loader: { '.csv': 'text' },
  define: { __VERSION__: JSON.stringify(pkg.version) },
});
const js = r.outputFiles[0].text.replace(/<\/script/gi, '<\\/script');
const css = readFileSync(join(AQUI, 'src/styles.css'), 'utf8');
const cssMin = DEV ? css : css.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\n\s*/g, '\n').replace(/\n+/g, '\n');

mkdirSync(join(SALIDA, 'icons'), { recursive: true });
writeFileSync(join(SALIDA, 'index.html'), `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>${TITULO}</title>
<meta name="description" content="${DESCRIPCION}">
<meta name="theme-color" content="${TEMA}">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="apple-mobile-web-app-title" content="${TITULO}">
<link rel="manifest" href="manifest.webmanifest">
<link rel="icon" href="icons/icon-192.png">
<link rel="apple-touch-icon" href="icons/apple-touch-icon.png">
<style>
${cssMin}
</style>
</head>
<body>
<div id="app"></div>
<noscript><p style="padding:24px;font-family:system-ui;color:#fff">Señas RD necesita JavaScript para funcionar.</p></noscript>
<script>${js}</script>
</body>
</html>
`);

writeFileSync(join(SALIDA, 'manifest.webmanifest'), JSON.stringify({
  name: 'Señas RD · Traductor de LSRD', short_name: 'Señas RD', description: DESCRIPCION, lang: 'es',
  start_url: './', scope: './', display: 'standalone', orientation: 'portrait',
  background_color: TEMA, theme_color: TEMA, categories: ['utilities', 'education'],
  icons: [
    { src: 'icons/icon-192.png', sizes: '192x192', type: 'image/png' },
    { src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png' },
    { src: 'icons/icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
  ],
  shortcuts: [
    { name: 'Emergencia', url: './#/emergencia' },
    { name: 'Conversación', url: './#/conversacion' },
  ],
}, null, 2));

// Íconos (se generan una sola vez; borre docs/icons para rehacerlos).
if (!existsSync(join(SALIDA, 'icons/icon-512.png'))) {
  const sharp = (await import('sharp')).default;
  const svg = (margen) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
<rect width="512" height="512" rx="${margen ? 0 : 112}" fill="#0F766E"/>
<g transform="translate(256 262) scale(${margen ? 0.72 : 0.92})" fill="none" stroke="#F2F5F9" stroke-width="30" stroke-linecap="round" stroke-linejoin="round">
<path d="M-70 120 L-70 -10 L-70 -120"/><path d="M-18 -8 L-18 -80"/><path d="M34 0 L34 -70"/><path d="M86 20 L86 -150"/>
<path d="M-70 40 L-150 -20"/><path d="M-70 120 Q-70 170 -10 170 L40 170 Q86 170 86 110 L86 20"/>
</g></svg>`);
  await sharp(svg(false)).resize(192, 192).png().toFile(join(SALIDA, 'icons/icon-192.png'));
  await sharp(svg(false)).resize(512, 512).png().toFile(join(SALIDA, 'icons/icon-512.png'));
  await sharp(svg(true)).resize(512, 512).png().toFile(join(SALIDA, 'icons/icon-maskable-512.png'));
  await sharp(svg(true)).resize(180, 180).png().toFile(join(SALIDA, 'icons/apple-touch-icon.png'));
}

// Detector MediaPipe holístico, alojado junto a la app. No se incluye el modelo "pesado".
const ORIGEN = join(AQUI, 'node_modules/@mediapipe/holistic');
const MP = join(SALIDA, 'mediapipe');
const ARCHIVOS = ['holistic.js', 'holistic.binarypb', 'holistic_solution_packed_assets.data', 'holistic_solution_packed_assets_loader.js',
  'holistic_solution_simd_wasm_bin.js', 'holistic_solution_simd_wasm_bin.wasm', 'holistic_solution_simd_wasm_bin.data',
  'holistic_solution_wasm_bin.js', 'holistic_solution_wasm_bin.wasm', 'pose_landmark_lite.tflite', 'pose_landmark_full.tflite'];
if (existsSync(ORIGEN)) {
  mkdirSync(MP, { recursive: true });
  for (const f of ARCHIVOS) if (existsSync(join(ORIGEN, f))) cpSync(join(ORIGEN, f), join(MP, f));
  writeFileSync(join(MP, 'LEEME.txt'), 'MediaPipe Holistic (paquete npm @mediapipe/holistic 0.5.1675471629), de Google, licencia Apache 2.0.\nhttps://github.com/google-ai-edge/mediapipe\nCopiado aquí por web/build.mjs para que la app funcione sin depender de otros servidores.\n');
} else if (!existsSync(join(MP, 'holistic.js'))) {
  console.warn('AVISO: falta el detector. Ejecute "npm install" en web/ y vuelva a construir.');
}

writeFileSync(join(SALIDA, '.nojekyll'), '');
writeFileSync(join(SALIDA, 'LEEME.txt'), 'Esta carpeta es la app web Señas RD ya construida. No la edite a mano: se genera con "npm run build" en web/.\nGitHub Pages la publica tal cual (Settings → Pages → rama main, carpeta /docs).\n');

// Caché para usar la app sin conexión. El nombre cambia cuando cambia cualquier archivo.
const huella = createHash('sha256');
const recorrer = (d) => readdirSync(d).sort().forEach((n) => { const p = join(d, n); if (statSync(p).isDirectory()) recorrer(p); else if (n !== 'sw.js') huella.update(n + statSync(p).size).update(n.endsWith('.html') ? readFileSync(p) : ''); });
recorrer(SALIDA);
const version = huella.digest('hex').slice(0, 10);
writeFileSync(join(SALIDA, 'sw.js'), `// Señas RD · caché para usar la app sin conexión. No guarda video, audio ni datos personales.
const CACHE = 'senas-rd-${version}';
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
  // El detector pesa unos 25 MB: se guarda la primera vez que se usa la cámara y después se sirve desde el teléfono.
  e.respondWith(caches.match(req).then((guardado) => guardado || fetch(req).then((r) => {
    if (r.ok && r.status === 200) { const cp = r.clone(); caches.open(CACHE).then((c) => c.put(req, cp)); }
    return r;
  })));
});
`);

const kb = (p) => (statSync(p).size / 1024).toFixed(0);
console.log(`docs/index.html ${kb(join(SALIDA, 'index.html'))} KB · caché ${version}`);
