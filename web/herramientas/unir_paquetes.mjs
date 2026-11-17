// Arma web/paquetes/base.json: las señas que vienen incluidas con la app para todas las personas.
//
//   node herramientas/unir_paquetes.mjs --autor "ANSORDO" archivo1.json archivo2.json …
//   npm run build        (publica el resultado en docs/paquetes/base.json)
//
// Cada archivo es un paquete guardado desde Ajustes → «Guardar en un archivo». Solo entran los
// que la persona autorizó al guardarlos: el paquete se publica junto con la app, a la vista de todos.
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as Paquete from '../src/nucleo/paquete.js';

/** @returns {{ paquete, resumen }} */
export function unir(paquetes, { autor = '', maximoPorSena = 12 } = {}) {
  const ejemplos = [], propias = new Map(), resumen = { aceptados: 0, sinAutorizacion: 0, errores: [], porSena: {} };
  paquetes.forEach((obj, k) => {
    if (!obj || !obj.consentimiento_entrenamiento) { resumen.sinAutorizacion++; return; }
    let r;
    try { r = Paquete.importar(obj); } catch (e) { resumen.errores.push(`paquete ${k + 1}: ${e.message}`); return; }
    resumen.aceptados++;
    resumen.errores.push(...r.errores);
    for (const p of r.propias) propias.set(p.glosa, p);
    r.ejemplos.forEach((e, i) => {
      const n = resumen.porSena[e.glosa] || 0;
      if (n >= maximoPorSena) return;
      resumen.porSena[e.glosa] = n + 1;
      ejemplos.push({ ...e, id: `b${k}-${i}` });
    });
  });
  const paquete = Paquete.exportar(ejemplos, [...propias.values()], { autor, consentimiento: true });
  return { paquete, resumen };
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const args = process.argv.slice(2);
  let autor = '';
  const rutas = [];
  for (let i = 0; i < args.length; i++) { if (args[i] === '--autor') autor = args[++i] || ''; else rutas.push(args[i]); }
  if (rutas.length === 0) { console.error('Uso: node herramientas/unir_paquetes.mjs [--autor NOMBRE] paquete.json …'); process.exit(2); }
  const { paquete, resumen } = unir(rutas.map((r) => JSON.parse(readFileSync(r, 'utf8'))), { autor });
  const destino = join(dirname(fileURLToPath(import.meta.url)), '..', 'paquetes', 'base.json');
  writeFileSync(destino, JSON.stringify(paquete));
  const senas = Object.keys(resumen.porSena);
  console.log(`${paquete.ejemplos.length} ejemplos de ${senas.length} señas → ${destino}`);
  console.log(`Paquetes aceptados: ${resumen.aceptados}; sin autorización (omitidos): ${resumen.sinAutorizacion}; errores: ${resumen.errores.length}`);
  const pocas = senas.filter((g) => resumen.porSena[g] < 3);
  if (pocas.length) console.log(`Con menos de 3 ejemplos (la app todavía no las reconocerá): ${pocas.join(', ')}`);
}
