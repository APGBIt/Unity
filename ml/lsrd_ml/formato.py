"""Formato LSRD-LM v1: qué puntos clave se guardan, en qué orden y en qué unidades.

Este archivo es la fuente de verdad del formato. La app Unity usa una copia
generada automáticamente (``FormatoLandmarks.g.cs``) con ``scripts/generar_csharp.py``;
una prueba verifica que ambas copias coinciden.

Cada cuadro de video se representa como una matriz ``[N_PUNTOS, 3]`` (x, y, z):

* Unidades "corregidas por aspecto": 1.0 = alto de la imagen.
  ``x = x_normalizado * ancho / alto``, ``y = y_normalizado``,
  ``z = z_normalizado * ancho / alto`` (MediaPipe usa la escala de x para z).
* Un punto ausente (mano fuera de cuadro, cara no detectada) vale ``(0, 0, 0)``.
* "Izquierda" y "derecha" son las de la persona según el detector holístico.
  Si la imagen está en espejo, el detector intercambia los lados; el modelo
  se entrena con espejo aleatorio, así que no depende de eso.
"""

from __future__ import annotations

VERSION = "LSRD-LM v1"

# --- Pose (MediaPipe Pose, 33 puntos): torso superior --------------------------
POSE_INDICES_MEDIAPIPE = (0, 11, 12, 13, 14, 15, 16)
POSE_NOMBRES = (
    "nariz",
    "hombro_izq",
    "hombro_der",
    "codo_izq",
    "codo_der",
    "muneca_izq",
    "muneca_der",
)

# --- Manos (MediaPipe Hands, 21 puntos cada una) ------------------------------
PUNTOS_MANO = 21
MANO_MUNECA = 0  # índice local de la muñeca
MANO_BASE_MEDIO = 9  # nudillo del dedo medio: define el tamaño de la mano

# --- Rostro (Face Mesh, 468/478 puntos): rasgos no manuales ------------------
# Derecha/izquierda de la persona según la malla canónica de MediaPipe.
CARA_INDICES_MEDIAPIPE = (
    # boca: comisuras, centro externo arriba/abajo, centro interno arriba/abajo, comisuras internas
    61, 291, 0, 17, 13, 14, 78, 308,
    # ceja derecha (externo → interno) y ceja izquierda (externo → interno)
    70, 63, 105, 66, 107, 300, 293, 334, 296, 336,
    # ojo derecho (externo, interno, párpado superior, inferior) y ojo izquierdo
    33, 133, 159, 145, 263, 362, 386, 374,
    # punta de la nariz, mentón, frente
    1, 152, 10,
)

# Pares simétricos de la malla facial (para el espejo horizontal).
_PARES_CARA = (
    (61, 291), (78, 308),
    (70, 300), (63, 293), (105, 334), (66, 296), (107, 336),
    (33, 263), (133, 362), (159, 386), (145, 374),
)

# --- Disposición del vector canónico -------------------------------------------
INICIO_POSE = 0
INICIO_MANO_IZQ = INICIO_POSE + len(POSE_INDICES_MEDIAPIPE)  # 7
INICIO_MANO_DER = INICIO_MANO_IZQ + PUNTOS_MANO  # 28
INICIO_CARA = INICIO_MANO_DER + PUNTOS_MANO  # 49
N_PUNTOS = INICIO_CARA + len(CARA_INDICES_MEDIAPIPE)  # 78
N_COORDENADAS = 3

# Índices canónicos de uso frecuente
IDX_NARIZ = INICIO_POSE + 0
IDX_HOMBRO_IZQ = INICIO_POSE + 1
IDX_HOMBRO_DER = INICIO_POSE + 2
IDX_MUNECA_POSE_IZQ = INICIO_POSE + 5
IDX_MUNECA_POSE_DER = INICIO_POSE + 6
IDX_MUNECA_IZQ = INICIO_MANO_IZQ + MANO_MUNECA
IDX_MUNECA_DER = INICIO_MANO_DER + MANO_MUNECA
IDX_CARA_REFERENCIA = INICIO_CARA + CARA_INDICES_MEDIAPIPE.index(1)  # punta de la nariz

# Longitud fija de la secuencia que recibe el modelo de señas
LONGITUD_SECUENCIA = 32

# Huesos para dibujar el esqueleto (índices canónicos)
_DEDOS = ((0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8),
          (5, 9), (9, 10), (10, 11), (11, 12), (9, 13), (13, 14), (14, 15), (15, 16),
          (13, 17), (0, 17), (17, 18), (18, 19), (19, 20))
HUESOS = tuple(
    [(IDX_HOMBRO_IZQ, IDX_HOMBRO_DER), (IDX_HOMBRO_IZQ, INICIO_POSE + 3), (INICIO_POSE + 3, IDX_MUNECA_POSE_IZQ),
     (IDX_HOMBRO_DER, INICIO_POSE + 4), (INICIO_POSE + 4, IDX_MUNECA_POSE_DER)]
    + [(INICIO_MANO_IZQ + a, INICIO_MANO_IZQ + b) for a, b in _DEDOS]
    + [(INICIO_MANO_DER + a, INICIO_MANO_DER + b) for a, b in _DEDOS]
)


def _construir_espejo() -> tuple[int, ...]:
    """Permutación de puntos que corresponde a un espejo horizontal (izquierda ↔ derecha)."""
    perm = list(range(N_PUNTOS))
    # pose: hombros, codos y muñecas
    for a, b in ((1, 2), (3, 4), (5, 6)):
        perm[INICIO_POSE + a], perm[INICIO_POSE + b] = INICIO_POSE + b, INICIO_POSE + a
    # manos completas
    for i in range(PUNTOS_MANO):
        perm[INICIO_MANO_IZQ + i] = INICIO_MANO_DER + i
        perm[INICIO_MANO_DER + i] = INICIO_MANO_IZQ + i
    # cara
    pos = {m: INICIO_CARA + k for k, m in enumerate(CARA_INDICES_MEDIAPIPE)}
    for a, b in _PARES_CARA:
        perm[pos[a]], perm[pos[b]] = pos[b], pos[a]
    return tuple(perm)


PERMUTACION_ESPEJO = _construir_espejo()

# Grupos (para máscaras de presencia)
GRUPOS = {
    "pose": (INICIO_POSE, INICIO_MANO_IZQ),
    "mano_izq": (INICIO_MANO_IZQ, INICIO_MANO_DER),
    "mano_der": (INICIO_MANO_DER, INICIO_CARA),
    "cara": (INICIO_CARA, N_PUNTOS),
}


def describir() -> dict:
    """Descripción serializable del formato (se exporta junto al modelo)."""
    return {
        "version": VERSION,
        "n_puntos": N_PUNTOS,
        "n_coordenadas": N_COORDENADAS,
        "longitud_secuencia": LONGITUD_SECUENCIA,
        "pose_indices_mediapipe": list(POSE_INDICES_MEDIAPIPE),
        "cara_indices_mediapipe": list(CARA_INDICES_MEDIAPIPE),
        "inicio": {
            "pose": INICIO_POSE,
            "mano_izq": INICIO_MANO_IZQ,
            "mano_der": INICIO_MANO_DER,
            "cara": INICIO_CARA,
        },
        "unidades": "x,y,z en alto de imagen (x*ancho/alto, y, z*ancho/alto); ausente = 0",
        "permutacion_espejo": list(PERMUTACION_ESPEJO),
    }
