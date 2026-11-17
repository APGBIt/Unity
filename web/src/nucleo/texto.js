// Utilidades de texto en español (las mismas reglas que el servidor y la app Unity).
export const PREGUNTAS = new Set(['QUE', 'DONDE', 'CUANDO', 'CUANTO', 'QUIEN', 'COMO', 'POR-QUE', 'CUAL']);

/** Palabras que la traducción literal omite. */
export const FUNCIONALES = new Set(('el la los las un una unos unas lo de del al a en y e o u que se le les me te nos su sus mi mis tu tus por para con sin ' +
  'es son era fue está están esta este estos estas ese esa eso muy como si ya pero porque cuando donde hay ser estar ha he han hemos va debe debo deben usted ustedes').split(' '));

/** Minúsculas, sin signos de puntuación, espacios simples. Conserva tildes y ñ. */
export function normalizar(texto) {
  return (texto || '').toLowerCase().replace(/[^\p{L}\p{N}\s]+/gu, ' ').replace(/\s+/g, ' ').trim();
}

/** Quita tildes y diéresis; conserva la ñ. */
export function sinTildes(texto) {
  return (texto || '').replace(/ñ/g, '\u0001').replace(/Ñ/g, '\u0002').normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/\u0001/g, 'ñ').replace(/\u0002/g, 'Ñ');
}

export const clave = (texto) => sinTildes(normalizar(texto));

/** Lector CSV con comillas (RFC 4180). Devuelve una lista de objetos por encabezado. */
export function leerCsv(texto) {
  const filas = [];
  let fila = [], campo = '', comillas = false;
  for (let i = 0; i < texto.length; i++) {
    const c = texto[i];
    if (comillas) {
      if (c === '"') { if (texto[i + 1] === '"') { campo += '"'; i++; } else comillas = false; } else campo += c;
      continue;
    }
    if (c === '"') comillas = true;
    else if (c === ',') { fila.push(campo); campo = ''; }
    else if (c === '\r' || c === '﻿') { /* se ignora */ }
    else if (c === '\n') { fila.push(campo); campo = ''; filas.push(fila); fila = []; }
    else campo += c;
  }
  if (campo.length > 0 || fila.length > 0) { fila.push(campo); filas.push(fila); }
  if (filas.length === 0) return [];
  const enc = filas[0].map((e) => e.trim());
  return filas.slice(1).filter((f) => !(f.length === 1 && f[0] === '')).map((f) => Object.fromEntries(enc.map((e, k) => [e, f[k] ?? ''])));
}
