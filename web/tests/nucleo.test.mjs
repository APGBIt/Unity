import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as F from '../src/nucleo/formato.g.js';
import { indices, remuestrear } from '../src/nucleo/remuestreo.js';
import { SegmentadorEnVivo, segmentarSecuencia } from '../src/nucleo/segmentacion.js';
import { empaquetar, espejar, manoActiva } from '../src/nucleo/empaquetador.js';
import { base64AFloats, floatsABase64 } from '../src/nucleo/base64.js';
import { ReconocedorPorEjemplos, distancia } from '../src/nucleo/reconocedor.js';
import { espejarRasgos, extraer } from '../src/nucleo/rasgos.js';
import { Vocabulario, leerFrases } from '../src/nucleo/vocabulario.js';
import { clave, leerCsv, normalizar, sinTildes } from '../src/nucleo/texto.js';
import { ConstructorFrase } from '../src/nucleo/frase.js';
import * as Paquete from '../src/nucleo/paquete.js';
import { agrupar, cargar } from './datos.mjs';

const raiz = new URL('../../', import.meta.url).pathname;
const oro = JSON.parse(readFileSync(raiz + 'unity/LSRD-AR/Assets/LSRD/Tests/Datos/vectores_oro.json', 'utf8'));
const csv = (n) => readFileSync(raiz + 'vocabulario/' + n, 'utf8');
const V = F.VALORES_POR_CUADRO;
const cuadrosDe = (b64) => { const p = base64AFloats(b64); const c = []; for (let i = 0; i < p.length; i += V) c.push(p.subarray(i, i + V)); return c; };
const datos = cargar(new URL('./datos/sintetico.json.gz', import.meta.url).pathname);

// ---------------------------------------------------------------- paridad con Python y C#

test('el formato generado coincide con el de los vectores de referencia', () => {
  assert.equal(oro.formato, F.VERSION);
  assert.equal(F.N_PUNTOS, 78);
  assert.equal(V, 234);
});

test('remuestreo: mismos índices que Python', () => {
  for (const c of oro.remuestreo) assert.deepEqual(indices(c.longitud, c.t), c.indices);
  assert.throws(() => indices(0));
});

test('segmentación: mismas mediciones y mismos segmentos que Python, cuadro a cuadro', () => {
  for (const c of oro.segmentacion.casos) {
    const r = segmentarSecuencia(cuadrosDe(c.cuadros_b64), c.tiempos_ms, oro.segmentacion.parametros);
    assert.equal(r.segmentos.length, c.segmentos.length, c.nombre);
    r.segmentos.forEach((s, i) => { assert.equal(s[0], c.segmentos[i][0], c.nombre); assert.equal(s[1], c.segmentos[i][1], c.nombre); });
    r.mediciones.forEach((m, i) => {
      assert.equal(m[0], !!c.mediciones[i][0], `${c.nombre} activo ${i}`);
      const esperado = c.mediciones[i][1] === -1 ? Infinity : c.mediciones[i][1]; // Python escribe −1 por «infinito»
      if (esperado === Infinity) assert.equal(m[1], Infinity); else assert.ok(Math.abs(m[1] - esperado) < 1e-9, `${c.nombre} velocidad ${i}`);
    });
  }
});

test('empaquetado de MediaPipe: idéntico bit a bit a Python', () => {
  for (const c of oro.empaquetado) {
    const pts = (b64, n) => { if (!b64) return null; const f = base64AFloats(b64); const l = []; for (let i = 0; i < f.length; i += n) l.push({ x: f[i], y: f[i + 1], z: f[i + 2], visibility: n === 4 ? f[i + 3] : undefined }); return l; };
    const r = empaquetar(pts(c.pose_b64, 4), pts(c.mano_izq_b64, 3), pts(c.mano_der_b64, 3), pts(c.cara_b64, 3), c.aspecto);
    assert.equal(floatsABase64(r), c.esperado_b64);
  }
});

test('mano activa: idéntica a Python', () => {
  for (const c of oro.mano_activa) {
    const r = manoActiva(base64AFloats(c.cuadro_b64));
    if (c.esperado_b64 === null) assert.equal(r, null); else assert.equal(floatsABase64(r), c.esperado_b64);
  }
});

test('base64 ida y vuelta', () => {
  const f = new Float32Array([0, 1.5, -2.25, 1e-7, 123456.75]);
  assert.deepEqual([...base64AFloats(floatsABase64(f))], [...f]);
  assert.throws(() => base64AFloats('AAA='));
});

// ---------------------------------------------------------------- reconocedor por ejemplos

const conocidas = datos.muestras.filter((m) => !m.desconocida);
const ensenarCon = (firmante, k) => {
  const rec = new ReconocedorPorEjemplos();
  const porClase = agrupar(conocidas.filter((m) => m.firmante === firmante), (m) => m.glosa);
  const ens = [], resto = [];
  for (const ms of porClase.values()) { ens.push(...ms.slice(0, k)); resto.push(...ms.slice(k)); }
  rec.fijar(ens);
  return { rec, resto };
};

test('una seña es idéntica a sí misma y distinta de otra', () => {
  const a = extraer(conocidas[0].sec), b = extraer(conocidas.find((m) => m.glosa !== conocidas[0].glosa).sec);
  assert.equal(distancia(a, a), 0);
  assert.ok(distancia(a, b) > 0.3);
  assert.equal(distancia(a, b), distancia(b, a));
});

test('con 3 ejemplos reconoce tomas nuevas de la misma persona y casi nunca se equivoca', () => {
  let bien = 0, mal = 0, n = 0;
  for (const f of ['P00', 'P01', 'P02']) {
    const { rec, resto } = ensenarCon(f, 3);
    for (const m of resto) { n++; const c = rec.clasificar(m.sec).candidatas[0]; if (c.prob >= 0.6) { if (c.glosa === m.glosa) bien++; else mal++; } }
  }
  assert.ok(bien / n >= 0.9, `acierta ${bien}/${n}`);
  assert.equal(mal, 0);
});

test('no acepta señas que nadie le enseñó', () => {
  let falsas = 0, n = 0;
  for (const f of ['P00', 'P01', 'P02']) {
    const rec = new ReconocedorPorEjemplos();
    rec.fijar(conocidas.filter((m) => m.firmante === f));
    for (const m of datos.muestras.filter((x) => x.desconocida && x.firmante === f)) { n++; const r = rec.clasificar(m.sec); if (r.candidatas[0].prob >= 0.6) falsas++; else assert.ok(r.ninguna > 0.2); }
  }
  assert.ok(falsas <= 1, `${falsas}/${n} falsas`);
});

test('reconoce a quien firma con la otra mano (espejo)', () => {
  const zurda = [...new Set(conocidas.filter((m) => m.zurdo).map((m) => m.firmante))];
  assert.equal(zurda.length, 1);
  const diestro = 'P00';
  const { rec } = ensenarCon(diestro, 5);
  const pruebas = conocidas.filter((m) => m.firmante === zurda[0]);
  const medir = (r) => pruebas.filter((m) => { const x = r.clasificar(m.sec); return x.candidatas[0].glosa === m.glosa && x.candidatas[0].prob >= 0.6; }).length;
  const con = medir(rec);
  const sinEspejo = new ReconocedorPorEjemplos({ aceptarEspejo: false });
  sinEspejo.clases = rec.clases; sinEspejo.radioTipico = rec.radioTipico;
  assert.ok(con / pruebas.length >= 0.85, `con espejo ${con}/${pruebas.length}`);
  assert.ok(medir(sinEspejo) < con, 'sin espejo debe reconocer menos');
  assert.ok(pruebas.some((m) => rec.clasificar(m.sec).espejo));
});

test('espejar los rasgos equivale a espejar los puntos', () => {
  const sec = conocidas[3].sec;
  const espejada = new Float32Array(sec.length);
  for (let i = 0; i < F.LONGITUD_SECUENCIA; i++) espejada.set(espejar(sec.subarray(i * V, (i + 1) * V)), i * V);
  const a = espejarRasgos(extraer(sec)), b = extraer(espejada);
  let max = 0;
  for (let i = 0; i < a.length; i++) max = Math.max(max, Math.abs(a[i] - b[i]));
  assert.ok(max < 1e-4, `diferencia ${max}`);
});

test('no depende de la distancia a la cámara ni del encuadre', () => {
  const { rec, resto } = ensenarCon('P01', 3);
  const m = resto[0];
  const lejos = new Float32Array(m.sec.length);
  for (let i = 0; i < m.sec.length; i += 3) {
    if (m.sec[i] === 0 && m.sec[i + 1] === 0 && m.sec[i + 2] === 0) continue;
    lejos[i] = m.sec[i] * 0.6 + 0.21; lejos[i + 1] = m.sec[i + 1] * 0.6 + 0.13; lejos[i + 2] = m.sec[i + 2] * 0.6;
  }
  const a = rec.clasificar(m.sec).candidatas[0], b = rec.clasificar(lejos).candidatas[0];
  assert.equal(b.glosa, a.glosa);
  assert.ok(Math.abs(a.distancia - b.distancia) < 1e-3);
});

test('sin nada enseñado no reconoce nada; con menos del mínimo de ejemplos la seña no participa', () => {
  const vacio = new ReconocedorPorEjemplos();
  assert.deepEqual(vacio.clasificar(conocidas[0].sec), { candidatas: [], ninguna: 1, espejo: false });
  const rec = new ReconocedorPorEjemplos({ minEjemplos: 3 });
  const s00 = conocidas.filter((m) => m.firmante === 'P00' && m.glosa === 'S00'), s01 = conocidas.filter((m) => m.firmante === 'P00' && m.glosa === 'S01');
  rec.fijar([...s00.slice(0, 3), ...s01.slice(0, 2)]);
  assert.equal(rec.cantidad('S01'), 2);
  assert.deepEqual(rec.clasificar(s01[3].sec).candidatas.map((c) => c.glosa), ['S00']);
  assert.ok(rec.clasificar(s01[3].sec).candidatas[0].prob < 0.35);
});

// ---------------------------------------------------------------- flujo en vivo

test('en vivo: el segmentador corta cada seña y el reconocedor la identifica', () => {
  const flujo = cargar(new URL('../e2e/datos/flujo.json.gz', import.meta.url).pathname);
  const cortar = (toma) => {
    const seg = new SegmentadorEnVivo();
    const salidas = [];
    for (let i = 0; i < toma.n; i++) { const r = seg.agregar(toma.cuadros.subarray(i * V, (i + 1) * V), toma.tiempos_ms[i]); if (r) salidas.push(r); }
    return salidas;
  };
  const porGlosa = agrupar(flujo.tomas.filter((t) => t.glosa !== 'DESCONOCIDA'), (t) => t.glosa);
  const ejemplos = [], pruebas = [];
  for (const [glosa, tomas] of porGlosa) {
    tomas.forEach((t, k) => {
      const cortes = cortar(t);
      assert.equal(cortes.length, 1, `${glosa} toma ${k}: se esperaba una seña`);
      (k < 4 ? ejemplos : pruebas).push({ glosa, sec: remuestrear(cortes[0].cuadros) });
    });
  }
  const rec = new ReconocedorPorEjemplos({ minEjemplos: 3 });
  rec.fijar(ejemplos);
  for (const p of pruebas) { const c = rec.clasificar(p.sec).candidatas[0]; assert.equal(c.glosa, p.glosa); assert.ok(c.prob >= 0.6, `${p.glosa} ${c.prob}`); }
  const rara = cortar(flujo.tomas.find((t) => t.glosa === 'DESCONOCIDA'));
  assert.equal(rara.length, 1);
  assert.ok(rec.clasificar(remuestrear(rara[0].cuadros)).candidatas[0].prob < 0.6);
});

// ---------------------------------------------------------------- texto, vocabulario y frases

test('texto: normalizar, tildes y CSV', () => {
  assert.equal(normalizar('  ¿Dónde   está el BAÑO? '), 'dónde está el baño');
  assert.equal(sinTildes('cédula pingüino año'), 'cedula pinguino año');
  assert.equal(clave('¿CÉDULA?'), 'cedula');
  assert.deepEqual(leerCsv('a,b\n"x, y","dijo ""hola"""\n\n1,2\n'), [{ a: 'x, y', b: 'dijo "hola"' }, { a: '1', b: '2' }]);
});

const vocab = () => new Vocabulario(leerCsv(csv('vocabulario_mvp.csv')), leerCsv(csv('cobertura_diccionario_oficial.csv')));

test('diccionario: búsqueda sin tildes, por tema y referencia oficial', () => {
  const v = vocab();
  assert.equal(v.consultar('').length, 193);
  assert.equal(v.consultar('¿CEDULA?')[0].glosa, 'CEDULA');
  assert.equal(v.consultar('dias')[0].glosa, 'DIA');
  assert.ok(v.consultar('muéstreme').some((e) => e.glosa === 'MOSTRAR'));
  assert.ok(v.consultar('', 'saludos').every((e) => e.categoria === 'saludos'));
  assert.equal(v.consultar('xilófono').length, 0);
  assert.equal(v.porGlosa.get('GRACIAS').urlOficial, 'https://diccionariolsrd.cc/diccionario/gracias/');
  assert.equal(v.porGlosa.get('CEDULA').tituloOficial, '');
});

test('español → glosas: frase más larga, números, deletreo y palabras funcionales', () => {
  const v = vocab();
  assert.deepEqual(v.literal('Buenos días, ¿necesita ayuda?'), ['BUENOS-DIAS', 'NECESITAR', 'AYUDAR']);
  assert.deepEqual(v.literal('Tome el turno 25'), ['DACT:tome', 'TURNO', 'NUM-2', 'NUM-5']);
  assert.deepEqual(v.literal('la cedula'), ['CEDULA']);
  const r = v.textoAGlosas('¿Dónde vive Yuderka?');
  assert.equal(r.pregunta, true);
  const ultimo = r.plan[r.plan.length - 1];
  assert.equal(ultimo.deletreo, true);
  assert.deepEqual(ultimo.letras, ['LETRA-Y', 'LETRA-U', 'LETRA-D', 'LETRA-E', 'LETRA-R', 'LETRA-K', 'LETRA-A']);
});

test('glosas → español: literal, con signos de pregunta cuando corresponde', () => {
  const v = vocab();
  assert.equal(v.glosasATexto(['YO', 'NECESITAR', 'CITA']), 'Yo necesitar cita.');
  assert.equal(v.glosasATexto(['BAÑO', 'DONDE']), '¿Baño dónde?');
  assert.equal(v.glosasATexto(['NUM-3', '_FONDO']), '3.');
  assert.equal(v.glosasATexto([]), '');
});

test('señas propias: se pueden añadir, buscar y usar en la traducción', () => {
  const v = vocab();
  const e = v.agregarPropia('  Señora  Pérez ');
  assert.equal(e.glosa, 'SEÑORA-PEREZ');
  assert.equal(e.categoria, 'propias');
  assert.equal(v.agregarPropia('señora pérez'), e);                 // no se duplica
  assert.equal(v.agregarPropia('Gracias').glosa, 'GRACIAS');        // ya existía en el vocabulario
  assert.deepEqual(v.literal('llame a la señora Pérez'), ['LLAMAR', 'SEÑORA-PEREZ']);
  assert.equal(v.glosasATexto(['SEÑORA-PEREZ']), 'Señora Pérez.');
  assert.equal(v.agregarPropia('   '), null);
  const v2 = new Vocabulario(leerCsv(csv('vocabulario_mvp.csv')), [], [{ glosa: 'SEÑORA-PEREZ', espanol: 'Señora Pérez' }]);
  assert.ok(v2.existe('SEÑORA-PEREZ'));
});

test('frases de mostrador y de emergencia', () => {
  const f = leerFrases(csv('frases_mvp.csv')), e = leerFrases(csv('emergencias.csv'));
  assert.equal(f.length, 50);
  assert.ok(e.filter((x) => x.direccion === 'sordo_a_oyente').length >= 8 && e.filter((x) => x.direccion === 'oyente_a_sordo').length >= 4);
  assert.ok(e.some((x) => x.espanol.includes('9-1-1')));
});

test('constructor de frase: dudosas, repetidas, correcciones', () => {
  const c = new ConstructorFrase();
  const r = (glosa, prob, otras = []) => ({ candidatas: [{ glosa, prob }, ...otras] });
  assert.equal(c.agregar(r('HOLA', 0.2), 0, 500), null);
  c.agregar(r('HOLA', 0.9), 0, 500);
  c.agregar(r('HOLA', 0.7), 700, 1200);                                 // repetida enseguida: se funde
  const dudosa = c.agregar(r('CITA', 0.45, [{ glosa: 'CEDULA', prob: 0.3 }, { glosa: 'COPIA', prob: 0.01 }]), 2500, 3000);
  assert.deepEqual(c.glosas, ['HOLA', 'CITA']);
  assert.ok(dudosa.dudosa && c.tieneDudas);
  assert.deepEqual(dudosa.alternativas.map((a) => a.glosa), ['CEDULA']);
  c.corregir(1, 'CEDULA');
  assert.deepEqual(c.glosas, ['HOLA', 'CEDULA']);
  assert.ok(!c.tieneDudas && c.senas[1].alternativas.some((a) => a.glosa === 'CITA'));
  c.agregar(r('HOLA', 0.9), 5000, 5500);                                // repetida más tarde: es otra seña
  assert.equal(c.senas.length, 3);
  c.deshacer(); c.quitar(0);
  assert.deepEqual(c.glosas, ['CEDULA']);
  c.limpiar();
  assert.ok(c.vacia);
});

// ---------------------------------------------------------------- paquete de señas

test('paquete: ida y vuelta, y rechaza lo que no es válido', () => {
  const ej = conocidas.slice(0, 3).map((m, i) => ({ id: 'ej-000' + i, glosa: 'HOLA', espanol: 'hola', sec: m.sec, creado: '2026-11-16T00:00:00Z', duracionMs: 900 }));
  const p = JSON.parse(JSON.stringify(Paquete.exportar(ej, [{ glosa: 'SEÑORA-PEREZ', espanol: 'Señora Pérez' }], { autor: 'Ana', consentimiento: true })));
  assert.equal(p.consentimiento_entrenamiento, true);
  assert.ok(!JSON.stringify(p).includes('video_b64'));
  const r = Paquete.importar(p);
  assert.equal(r.ejemplos.length, 3);
  assert.deepEqual([...r.ejemplos[1].sec], [...ej[1].sec]);
  assert.deepEqual(r.propias, [{ glosa: 'SEÑORA-PEREZ', espanol: 'Señora Pérez' }]);

  assert.throws(() => Paquete.importar({ tipo: 'otra-cosa' }));
  assert.throws(() => Paquete.importar({ ...p, formato: 'LSRD-LM v0' }));
  const malo = JSON.parse(JSON.stringify(p));
  malo.ejemplos[0].glosa = '<script>';
  malo.ejemplos[1].sec_b64 = floatsABase64(new Float32Array(10));
  const nan = new Float32Array(ej[2].sec); nan[5] = NaN;
  malo.ejemplos[2].sec_b64 = floatsABase64(nan);
  malo.propias.push({ glosa: 'x y', espanol: 'x' });
  const rm = Paquete.importar(malo);
  assert.equal(rm.ejemplos.length, 0);
  assert.equal(rm.errores.length, 3);
  assert.equal(rm.propias.length, 1);
});
