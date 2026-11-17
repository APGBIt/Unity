// Paquete de señas: copia de seguridad y forma de pasar las señas enseñadas de un teléfono a otro.
// Usa el mismo formato de puntos clave que el canal de entrenamiento (LSRD-LM v1, float32 en base64).
import { LONGITUD_SECUENCIA, VALORES_POR_CUADRO, VERSION } from './formato.g.js';
import { base64AFloats, floatsABase64 } from './base64.js';

export const TIPO = 'senas-rd/paquete';
const TAM = LONGITUD_SECUENCIA * VALORES_POR_CUADRO;

/**
 * @param ejemplos [{ id, glosa, espanol, sec: Float32Array, creado, duracionMs }]
 * @param propias  [{ glosa, espanol }]
 */
export function exportar(ejemplos, propias = [], meta = {}) {
  return {
    tipo: TIPO, version: 1, formato: VERSION, longitud: LONGITUD_SECUENCIA,
    creado: meta.creado || new Date().toISOString(),
    autor: meta.autor || '',
    consentimiento_entrenamiento: !!meta.consentimiento,
    nota: 'Solo puntos clave del cuerpo y las manos. No contiene video ni audio.',
    propias: propias.map((p) => ({ glosa: p.glosa, espanol: p.espanol })),
    ejemplos: ejemplos.map((e) => ({
      id: e.id, glosa: e.glosa, espanol: e.espanol || '', creado: e.creado || '', duracion_ms: Math.round(e.duracionMs || 0),
      sec_b64: floatsABase64(e.sec),
    })),
  };
}

/** Valida un paquete y devuelve { ejemplos, propias, errores }. No confía en nada de lo que trae. */
export function importar(obj) {
  const errores = [];
  if (!obj || obj.tipo !== TIPO) throw new Error('El archivo no es un paquete de señas de Señas RD.');
  if (obj.formato !== VERSION) throw new Error(`El paquete usa otro formato de puntos clave (${obj.formato}).`);
  const glosaValida = (g) => typeof g === 'string' && /^[A-ZÑ0-9][A-ZÑ0-9_-]{0,59}$/.test(g);
  const propias = (Array.isArray(obj.propias) ? obj.propias : [])
    .filter((p) => p && glosaValida(p.glosa) && typeof p.espanol === 'string' && p.espanol.trim())
    .map((p) => ({ glosa: p.glosa, espanol: p.espanol.trim().slice(0, 80) }));
  const ejemplos = [];
  for (const e of Array.isArray(obj.ejemplos) ? obj.ejemplos.slice(0, 5000) : []) {
    try {
      if (!glosaValida(e.glosa)) throw new Error('glosa inválida');
      const sec = base64AFloats(String(e.sec_b64));
      if (sec.length !== TAM) throw new Error('tamaño incorrecto');
      for (let i = 0; i < sec.length; i++) if (!Number.isFinite(sec[i]) || Math.abs(sec[i]) > 50) throw new Error('valores fuera de rango');
      ejemplos.push({
        id: typeof e.id === 'string' && /^[\w-]{4,64}$/.test(e.id) ? e.id : null, glosa: e.glosa,
        espanol: typeof e.espanol === 'string' ? e.espanol.slice(0, 80) : '', sec,
        creado: typeof e.creado === 'string' ? e.creado.slice(0, 40) : '', duracionMs: Number(e.duracion_ms) || 0,
      });
    } catch (err) {
      errores.push(`${e && e.glosa ? e.glosa : 'ejemplo'}: ${err.message}`);
    }
  }
  return { ejemplos, propias, errores, autor: typeof obj.autor === 'string' ? obj.autor.slice(0, 80) : '' };
}
