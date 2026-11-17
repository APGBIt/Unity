// Prueba de punta a punta en Chromium con un teléfono simulado.
//   node e2e/run.mjs            → cámara simulada con puntos clave sintéticos (enseñar, reconocer, conversar)
//   node e2e/run.mjs camara FOTO.y4m → cámara falsa con una imagen real: comprueba que MediaPipe arranca dentro de la app
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { spawn } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT || 'playwright');
const AQUI = dirname(fileURLToPath(import.meta.url));
const FOTOS = join(AQUI, 'capturas/');
mkdirSync(FOTOS, { recursive: true });
const PUERTO = 8766;
const MODO = process.argv[2] || 'simulada';
const servidor = spawn('python3', ['-m', 'http.server', String(PUERTO), '--directory', join(AQUI, '..', '..', 'docs')], { stdio: 'ignore' });
await new Promise((r) => setTimeout(r, 900));

const errores = [];
let fallos = 0;
const args = ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream', '--autoplay-policy=no-user-gesture-required',
  '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'];
if (MODO === 'camara' && process.argv[3]) args.push(`--use-file-for-fake-video-capture=${process.argv[3]}`);
const navegador = await chromium.launch({ args });
const ctx = await navegador.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, permissions: ['camera'], acceptDownloads: true, locale: 'es-DO' });
const page = await ctx.newPage();
page.on('pageerror', (e) => errores.push('pageerror: ' + e.message));
page.on('console', (m) => { if (m.type() === 'error' && !/GL Driver|GPU stall|favicon/.test(m.text())) errores.push('console: ' + m.text().slice(0, 300)); });

const foto = async (nombre) => { await page.waitForTimeout(350); await page.screenshot({ path: `${FOTOS}${nombre}.png` }); };
const paso = async (titulo, fn) => {
  try { await fn(); console.log('ok   ', titulo); } catch (e) { fallos++; console.log('FALLA', titulo, '—', String(e.message).split('\n')[0]); await foto('falla-' + titulo.replace(/\W+/g, '_').slice(0, 40)); }
};
const esperar = (fn, arg, ms = 15000) => page.waitForFunction(fn, arg, { timeout: ms, polling: 100 });
const url = `http://localhost:${PUERTO}/index.html`;
const textoDe = (sel) => page.locator(sel).first().innerText();

if (MODO === 'camara') {
  await page.goto(url + '#/conversacion');
  await paso('MediaPipe arranca dentro de la app con una imagen real', async () => {
    await esperar(() => { const c = document.querySelector('.chip-camara'); return c && /Manos|Firmando|No veo/.test(c.textContent); }, null, 120000);
    console.log('      estado de la cámara:', await textoDe('.chip-camara'));
    const pintado = await page.evaluate(() => { const c = document.querySelector('canvas.esqueleto'); const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data; let n = 0; for (let i = 3; i < d.length; i += 4) if (d[i] > 0) n++; return n; });
    if (pintado < 50) throw new Error('no se dibujó el esqueleto sobre el video');
    console.log('      píxeles del esqueleto dibujados:', pintado);
  });
  await foto('camara-real');
} else {
  const flujo = gunzipSync(readFileSync(join(AQUI, 'datos/flujo.json.gz'))).toString('utf8');
  await page.addInitScript(`(() => {
    const d = ${flujo};
    const V = 234;
    const tomas = d.tomas.map((t) => { const b = atob(t.cuadros); const u = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) u[i] = b.charCodeAt(i);
      const q = new Int16Array(u.buffer); const cs = []; for (let i = 0; i < t.n; i++) { const c = new Float32Array(V); for (let k = 0; k < V; k++) c[k] = q[i * V + k] / d.escala; cs.push(c); } return { glosa: t.glosa, cuadros: cs }; });
    const reposo = tomas[0].cuadros[0];
    let cola = [], reloj = 1000;
    window.__senasSimulacion = {
      aspecto: d.aspecto, tomas: tomas.map((t) => t.glosa),
      reproducir(i) { cola = cola.concat(tomas[i].cuadros); },
      get pendiente() { return cola.length; },
      siguiente() { reloj += 1000 / 30; return { cuadro: cola.length ? cola.shift() : reposo, t: reloj }; },
    };
    // Voz simulada: se anota lo que la app manda a decir.
    window.__dicho = [];
    window.SpeechSynthesisUtterance = function (t) { this.text = t; };
    Object.defineProperty(window, 'speechSynthesis', { value: { speak(u) { if (u.text.trim()) window.__dicho.push(u.text); }, cancel() {}, getVoices() { return [{ name: 'Simulada', lang: 'es-DO' }]; } }, configurable: true });
  })();`);
  const tomasDe = (g) => page.evaluate((glosa) => window.__senasSimulacion.tomas.map((x, i) => (x === glosa ? i : -1)).filter((i) => i >= 0), g);
  const firmar = async (i) => { await page.evaluate((k) => window.__senasSimulacion.reproducir(k), i); await esperar(() => window.__senasSimulacion.pendiente === 0); await page.waitForTimeout(700); };

  await page.goto(url);
  await esperar(() => window.__senasLista === true);
  await paso('inicio: sin señas enseñadas lo dice claramente', async () => {
    if (!(await textoDe('.tarjeta.estado')).includes('Todavía no reconoce')) throw new Error('falta el aviso');
  });
  await foto('01-inicio');

  const ensenar = async (palabra, glosaSim, cuantas) => {
    await page.goto(url + '#/ensenar');
    await page.fill('input[type=search]', palabra);
    await page.locator('.renglon').first().click();
    await page.waitForSelector('.contador');
    await page.locator('.btn.grande').click();
    const idx = (await tomasDe(glosaSim)).slice(0, cuantas);
    for (let k = 0; k < idx.length; k++) { await firmar(idx[k]); await esperar((n) => document.querySelector('.contador strong').textContent === String(n), k + 1); }
    return idx;
  };

  await paso('enseñar «hola» con 4 ejemplos', async () => {
    await ensenar('hola', 'S00', 4);
    if (!(await textoDe('.contador')).includes('lista')) throw new Error('no quedó lista');
    await foto('02-ensenar-grabando');
    await page.locator('.btn.grande').click(); // terminar
  });
  await paso('probar la seña recién enseñada', async () => {
    const todas = await tomasDe('S00');
    await firmar(todas[4]);
    await esperar(() => /Reconoció «hola»/.test(document.querySelector('.resultado-prueba').textContent));
    await foto('03-ensenar-prueba');
  });
  await paso('enseñar «gracias» y «cédula»', async () => {
    await ensenar('gracias', 'S01', 4); await page.locator('.btn.grande').click();
    await ensenar('cedula', 'S02', 4); await page.locator('.btn.grande').click();
  });
  await paso('la lista muestra el avance', async () => {
    await page.goto(url + '#/ensenar');
    await page.locator('.btn.pestana', { hasText: 'Listas' }).click();
    const n = await page.locator('.renglon').count();
    if (n !== 3) throw new Error(`se esperaban 3 señas listas y hay ${n}`);
    await foto('04-ensenar-lista');
  });

  await paso('conversación: dos señas → frase → la dice sola al bajar las manos', async () => {
    await page.goto(url + '#/conversacion');
    await page.waitForSelector('.chip-camara');
    // Las dos señas seguidas, sin pausa larga entre ellas (la cámara simulada corre al doble de velocidad).
    const [a, b] = [(await tomasDe('S00'))[5], (await tomasDe('S02'))[5]];
    await page.evaluate(([x, y]) => { window.__senasSimulacion.reproducir(x); window.__senasSimulacion.reproducir(y); }, [a, b]);
    await esperar(() => document.querySelectorAll('.panel-sordo .chip').length === 2);
    const glosas = await page.$$eval('.panel-sordo .chip', (cs) => cs.map((c) => c.dataset.glosa));
    if (glosas.join(' ') !== 'HOLA CEDULA') throw new Error('reconoció ' + glosas.join(' '));
    await esperar(() => window.__dicho.includes('Hola cédula.'));
    await foto('05-conversacion');
  });
  await paso('una seña que nadie enseñó no se acepta', async () => {
    const antes = await page.$$eval('.panel-sordo .chip', (cs) => cs.map((c) => c.dataset.glosa).join(' '));
    await firmar((await tomasDe('DESCONOCIDA'))[0]);
    await esperar(() => /No reconocí/.test(document.querySelector('.aviso')?.textContent || ''));
    const despues = await page.$$eval('.panel-sordo .chip', (cs) => cs.map((c) => c.dataset.glosa).join(' '));
    if (despues !== antes) throw new Error(`cambió la frase: «${antes}» → «${despues}»`);
  });
  await paso('corregir y borrar', async () => {
    await page.locator('.panel-sordo .chip').first().click();
    await page.locator('.opciones .btn', { hasText: 'Quitar' }).click();
    if ((await page.locator('.panel-sordo .chip').count()) !== 1) throw new Error('no quitó la seña');
    await page.locator('.btn', { hasText: 'Borrar' }).first().click();
    if ((await page.locator('.panel-sordo .chip').count()) !== 0) throw new Error('no borró');
  });
  await paso('respuesta escrita → avatar con las señas enseñadas y palabras escritas', async () => {
    await page.fill('.fila-campo .campo', 'Hola, ¿tiene su cédula? Gracias');
    await page.locator('.btn', { hasText: 'Mostrar' }).click();
    await page.waitForSelector('.respuesta:not([hidden])');
    const pasos = await page.$$eval('.respuesta .chip.paso', (cs) => cs.map((c) => c.textContent + (c.classList.contains('sin-clip') ? '*' : '')));
    console.log('      plan del avatar:', pasos.join(' · '));
    if (!pasos.includes('hola') || !pasos.includes('cédula') || !pasos.includes('gracias')) throw new Error('faltan señas enseñadas en el plan');
    await page.waitForTimeout(900);
    await foto('06-respuesta-avatar');
    await page.locator('.respuesta .btn', { hasText: 'Cerrar' }).click();
  });
  await paso('frases rápidas de la persona sorda se dicen en voz alta', async () => {
    await page.locator('.btn', { hasText: 'Frases rápidas' }).click();
    await page.locator('.btn.pestana', { hasText: 'Dice la persona sorda' }).click();
    const frase = await page.locator('.caja .renglon').first().innerText();
    await page.locator('.caja .renglon').first().click();
    await esperar((f) => window.__dicho.includes(f), frase);
  });
  await paso('emergencia: muestra la frase en grande y la dice', async () => {
    await page.goto(url + '#/emergencia');
    await page.locator('.frase-emergencia').first().click();
    await page.waitForSelector('.cartel:not([hidden])');
    await esperar(() => window.__dicho.includes('Necesito ayuda urgente.'));
    await foto('07-emergencia');
  });
  await paso('diccionario: búsqueda sin tildes y ficha con la seña enseñada', async () => {
    await page.goto(url + '#/diccionario');
    await page.fill('input[type=search]', 'CEDULA');
    await page.locator('.renglon').first().click();
    await page.waitForSelector('.ficha:not([hidden])');
    if (!(await textoDe('.ficha')).includes('4 ejemplos')) throw new Error('la ficha no muestra los ejemplos');
    await page.waitForTimeout(500);
    await foto('08-diccionario-ficha');
  });
  let paquete = null;
  await paso('ajustes: guardar las señas en un archivo', async () => {
    await page.goto(url + '#/ajustes');
    await foto('09-ajustes');
    await page.locator('.btn', { hasText: 'Guardar en un archivo' }).click();
    const [descarga] = await Promise.all([page.waitForEvent('download'), page.locator('.caja .btn.primario').click()]);
    paquete = JSON.parse(readFileSync(await descarga.path(), 'utf8'));
    if (paquete.ejemplos.length !== 12 || paquete.tipo !== 'senas-rd/paquete') throw new Error('paquete incompleto');
    if (paquete.consentimiento_entrenamiento !== false) throw new Error('el consentimiento debe estar apagado por defecto');
  });
  await paso('las señas siguen ahí al cerrar y volver a abrir la app', async () => {
    await page.goto(url);
    await page.reload();
    await esperar(() => window.__senasLista === true);
    if (!(await textoDe('.tarjeta.estado')).includes('3 señas listas')) throw new Error(await textoDe('.tarjeta.estado'));
    await foto('10-inicio-con-senas');
  });
  await paso('borrar todo y recuperar desde el archivo', async () => {
    await page.goto(url + '#/ajustes');
    await page.locator('.btn', { hasText: 'Borrar todas las señas' }).click();
    await page.locator('.caja .btn.peligro').click();
    await esperar(() => /^0 ejemplos/.test(document.querySelector('.ajustes .nota').textContent));
    await page.setInputFiles('input[type=file]', { name: 'senas.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(paquete)) });
    await esperar(() => /^12 ejemplos/.test(document.querySelector('.ajustes .nota').textContent));
  });
  await paso('funciona sin conexión después de la primera visita', async () => {
    await page.goto(url);
    await page.evaluate(() => navigator.serviceWorker.ready);
    await page.waitForTimeout(800);
    await ctx.setOffline(true);
    await page.reload();
    await esperar(() => window.__senasLista === true);
    await page.goto(url + '#/emergencia');
    await page.waitForSelector('.frase-emergencia');
    await ctx.setOffline(false);
  });
}

await navegador.close();
servidor.kill();
for (const e of errores) console.log('error en la página →', e);
console.log(fallos || errores.length ? `\n${fallos} pasos fallidos, ${errores.length} errores en la página` : '\nTodo en orden.');
process.exit(fallos || errores.length ? 1 : 0);
