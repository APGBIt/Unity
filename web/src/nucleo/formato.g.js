// Archivo generado por ml/scripts/generar_javascript.py a partir de lsrd_ml/formato.py.
// No lo edite a mano: cambie formato.py y vuelva a generar.

export const VERSION = "LSRD-LM v1";
export const POSE_INDICES_MEDIAPIPE = [0, 11, 12, 13, 14, 15, 16];
export const CARA_INDICES_MEDIAPIPE = [61, 291, 0, 17, 13, 14, 78, 308, 70, 63, 105, 66, 107, 300, 293, 334, 296, 336, 33, 133, 159, 145, 263, 362, 386, 374, 1, 152, 10];
export const PUNTOS_MANO = 21;
export const MANO_MUNECA = 0;
export const MANO_BASE_MEDIO = 9;
export const INICIO_POSE = 0;
export const INICIO_MANO_IZQ = 7;
export const INICIO_MANO_DER = 28;
export const INICIO_CARA = 49;
export const N_PUNTOS = 78;
export const N_COORDENADAS = 3;
export const VALORES_POR_CUADRO = 234;
export const IDX_NARIZ = 0;
export const IDX_HOMBRO_IZQ = 1;
export const IDX_HOMBRO_DER = 2;
export const IDX_MUNECA_POSE_IZQ = 5;
export const IDX_MUNECA_POSE_DER = 6;
export const IDX_MUNECA_IZQ = 7;
export const IDX_MUNECA_DER = 28;
export const LONGITUD_SECUENCIA = 32;
export const HUESOS = [[1, 2], [1, 3], [3, 5], [2, 4], [4, 6], [7, 8], [8, 9], [9, 10], [10, 11], [7, 12], [12, 13], [13, 14], [14, 15], [12, 16], [16, 17], [17, 18], [18, 19], [16, 20], [20, 21], [21, 22], [22, 23], [20, 24], [7, 24], [24, 25], [25, 26], [26, 27], [28, 29], [29, 30], [30, 31], [31, 32], [28, 33], [33, 34], [34, 35], [35, 36], [33, 37], [37, 38], [38, 39], [39, 40], [37, 41], [41, 42], [42, 43], [43, 44], [41, 45], [28, 45], [45, 46], [46, 47], [47, 48]];
export const PERMUTACION_ESPEJO = [0, 2, 1, 4, 3, 6, 5, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 50, 49, 51, 52, 53, 54, 56, 55, 62, 63, 64, 65, 66, 57, 58, 59, 60, 61, 71, 72, 73, 74, 67, 68, 69, 70, 75, 76, 77];
export const SEGMENTACION = {"inicio_ms": 100.0, "fin_ms": 250.0, "min_ms": 250.0, "max_ms": 3000.0, "quietud_ms": 400.0, "umbral_velocidad": 0.5, "linea_reposo": 1.5, "usar_quietud": true, "factor_mano_a_hombros": 3.5};
