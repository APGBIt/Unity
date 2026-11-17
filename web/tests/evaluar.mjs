// Mide el reconocedor por ejemplos con datos sintéticos (no son LSRD).
//   node tests/evaluar.mjs [ruta/sintetico.json.gz]
import { ReconocedorPorEjemplos } from '../src/nucleo/reconocedor.js';
import { agrupar, cargar } from './datos.mjs';

const ruta = process.argv[2] || new URL('./datos/sintetico.json.gz', import.meta.url).pathname;
const UMBRAL = Number(process.env.UMBRAL || 0.6);
const d = cargar(ruta);
const conocidas = d.muestras.filter((m) => !m.desconocida);
const desconocidas = d.muestras.filter((m) => m.desconocida);
const firmantes = [...new Set(d.muestras.map((m) => m.firmante))];
const pct = (a, b) => (b ? `${((100 * a) / b).toFixed(1)} %` : '—');

function probar(rec, pruebas, extranas) {
  let bien = 0, mal = 0, sinRespuesta = 0, falsas = 0, ms = 0;
  for (const m of pruebas) {
    const t0 = performance.now();
    const r = rec.clasificar(m.sec);
    ms += performance.now() - t0;
    const c = r.candidatas[0];
    if (!c || c.prob < UMBRAL) sinRespuesta++;
    else if (c.glosa === m.glosa) bien++;
    else mal++;
  }
  for (const m of extranas) { const c = rec.clasificar(m.sec).candidatas[0]; if (c && c.prob >= UMBRAL) falsas++; }
  return { n: pruebas.length, bien, mal, sinRespuesta, falsas, nExtranas: extranas.length, ms: ms / Math.max(1, pruebas.length) };
}
const sumar = (rs) => rs.reduce((a, r) => { for (const k in r) a[k] = (a[k] || 0) + r[k]; return a; }, {});
const linea = (titulo, r, n) => console.log(`${titulo.padEnd(54)} acierta ${pct(r.bien, r.n).padStart(7)} · se equivoca ${pct(r.mal, r.n).padStart(6)} · no responde ${pct(r.sinRespuesta, r.n).padStart(6)} · acepta señas no enseñadas ${pct(r.falsas, r.nExtranas).padStart(6)} · ${(r.ms / n).toFixed(1)} ms`);

console.log(`${d.nota} ${conocidas.length} tomas de ${new Set(conocidas.map((m) => m.glosa)).size} señas, ${firmantes.length} personas. Umbral ${UMBRAL}.\n`);

for (const k of [1, 2, 3, 5]) {
  const rs = firmantes.map((f) => {
    const porClase = agrupar(conocidas.filter((m) => m.firmante === f), (m) => m.glosa);
    const ens = [], pru = [];
    for (const ms of porClase.values()) { ens.push(...ms.slice(0, k)); pru.push(...ms.slice(k)); }
    const rec = new ReconocedorPorEjemplos();
    rec.fijar(ens);
    return probar(rec, pru, desconocidas.filter((m) => m.firmante === f));
  });
  linea(`La misma persona, ${k} ejemplo(s) por seña`, sumar(rs), rs.length);
}

{
  const rs = firmantes.map((f) => {
    const rec = new ReconocedorPorEjemplos();
    const porClase = agrupar(conocidas.filter((m) => m.firmante === f), (m) => m.glosa);
    rec.fijar([...porClase.values()].flatMap((ms) => ms.slice(0, 5)));
    return probar(rec, conocidas.filter((m) => m.firmante !== f), desconocidas.filter((m) => m.firmante !== f));
  });
  linea('Otra persona (enseñó una sola, 5 ejemplos)', sumar(rs), rs.length);
}
{
  const rs = firmantes.map((f) => {
    const rec = new ReconocedorPorEjemplos();
    const otros = conocidas.filter((m) => m.firmante !== f);
    const porClaseYPersona = agrupar(otros, (m) => m.glosa + '|' + m.firmante);
    rec.fijar([...porClaseYPersona.values()].flatMap((ms) => ms.slice(0, 3)));
    return probar(rec, conocidas.filter((m) => m.firmante === f), desconocidas.filter((m) => m.firmante === f));
  });
  linea('Otra persona (enseñaron tres, 3 ejemplos cada una)', sumar(rs), rs.length);
}
