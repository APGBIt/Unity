"""Generador de datos sintéticos para PROBAR el canal técnico (no para entrenar el producto).

Produce secuencias con la misma forma que las reales (formato LSRD-LM v1):
cuerpos de distintas proporciones, manos con formas y trayectorias por clase,
expresiones faciales, ruido, pérdidas del detector, zurdos y entradas/salidas
de las manos del cuadro. Sirve para comprobar de punta a punta que el
entrenamiento, la evaluación por firmante, la exportación ONNX y la paridad
con C# funcionan antes de tener grabaciones reales.

Un modelo entrenado con esto NO reconoce LSRD.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np

from . import formato as F
from .datos import Muestra

# Posición de cada punto facial del subconjunto (relativa a la nariz, en unidades de tamaño de cara)
_CARA_BASE = {
    61: (-0.17, 0.22), 291: (0.17, 0.22), 0: (0.0, 0.17), 17: (0.0, 0.29), 13: (0.0, 0.21), 14: (0.0, 0.24),
    78: (-0.13, 0.225), 308: (0.13, 0.225),
    70: (-0.36, -0.30), 63: (-0.29, -0.34), 105: (-0.22, -0.35), 66: (-0.15, -0.34), 107: (-0.08, -0.31),
    300: (0.36, -0.30), 293: (0.29, -0.34), 334: (0.22, -0.35), 296: (0.15, -0.34), 336: (0.08, -0.31),
    33: (-0.32, -0.18), 133: (-0.12, -0.18), 159: (-0.22, -0.22), 145: (-0.22, -0.14),
    263: (0.32, -0.18), 362: (0.12, -0.18), 386: (0.22, -0.22), 374: (0.22, -0.14),
    1: (0.0, 0.0), 152: (0.0, 0.48), 10: (0.0, -0.62),
}
_CEJAS = {70, 63, 105, 66, 107, 300, 293, 334, 296, 336}
_LABIO_INF = {17, 14}


@dataclass
class Firmante:
    id: str
    aspecto: float
    cx: float
    cy: float
    ancho: float  # ancho de hombros (alto de imagen = 1)
    tam_mano: float  # en anchos de hombro
    tam_cara: float  # en anchos de hombro
    zurdo: bool
    velocidad: float
    amplitud: float
    estilo: np.ndarray = field(default_factory=lambda: np.zeros(4))


@dataclass
class ClaseSintetica:
    glosa: str
    puntos: np.ndarray  # [K, 2] trayectoria de la muñeca dominante (anchos de hombro, origen = centro de hombros)
    forma_ini: np.ndarray  # [6] flexión de 5 dedos + separación
    forma_fin: np.ndarray
    angulo_ini: float
    angulo_fin: float
    dos_manos: int  # 0 = una mano, 1 = simétrica, 2 = base fija
    cejas: float
    boca: float
    repeticiones: int
    duracion_s: float
    fondo: bool = False


def nuevo_firmante(rng: np.random.Generator, ident: str) -> Firmante:
    aspecto = float(rng.choice([0.75, 4 / 3, 16 / 9 * 0.75]))
    ancho = float(rng.uniform(0.26, 0.40))
    return Firmante(
        id=ident,
        aspecto=aspecto,
        cx=aspecto / 2 + float(rng.uniform(-0.05, 0.05)),
        cy=float(rng.uniform(0.55, 0.68)),
        ancho=ancho,
        tam_mano=float(rng.uniform(0.27, 0.36)),
        tam_cara=float(rng.uniform(0.5, 0.62)),
        zurdo=bool(rng.random() < 0.15),
        velocidad=float(rng.uniform(0.8, 1.25)),
        amplitud=float(rng.uniform(0.88, 1.12)),
        estilo=rng.normal(0, 0.06, size=4),
    )


def _forma_aleatoria(rng: np.random.Generator) -> np.ndarray:
    flex = rng.choice([0.0, 0.0, 0.5, 1.0, 1.0], size=5).astype(float)
    sep = float(rng.uniform(0, 1))
    return np.concatenate([flex, [sep]])


def nueva_clase(rng: np.random.Generator, glosa: str) -> ClaseSintetica:
    k = int(rng.integers(2, 5))
    puntos = np.stack([rng.uniform(-0.5, 1.1, size=k), rng.uniform(-1.3, 0.9, size=k)], axis=1)
    return ClaseSintetica(
        glosa=glosa,
        puntos=puntos,
        forma_ini=_forma_aleatoria(rng),
        forma_fin=_forma_aleatoria(rng) if rng.random() < 0.4 else None,
        angulo_ini=float(rng.uniform(-np.pi * 0.9, -np.pi * 0.1)),
        angulo_fin=float(rng.uniform(-np.pi * 0.9, -np.pi * 0.1)),
        dos_manos=int(rng.choice([0, 0, 1, 2])),
        cejas=float(rng.choice([0.0, 0.0, 0.0, 1.0, -0.6])),
        boca=float(rng.choice([0.0, 0.0, 0.0, 1.0])),
        repeticiones=int(rng.choice([1, 1, 2])),
        duracion_s=float(rng.uniform(0.6, 1.2)),
    )


def clase_fondo() -> ClaseSintetica:
    return ClaseSintetica(
        glosa="_FONDO", puntos=np.zeros((2, 2)), forma_ini=np.zeros(6), forma_fin=None,
        angulo_ini=-np.pi / 2, angulo_fin=-np.pi / 2, dos_manos=0, cejas=0.0, boca=0.0,
        repeticiones=1, duracion_s=1.0, fondo=True,
    )


def nueva_letra(rng: np.random.Generator, glosa: str) -> ClaseSintetica:
    """Letra estática del alfabeto manual: forma fija cerca del hombro dominante."""
    forma = _forma_aleatoria(rng)
    ang = float(rng.choice([-np.pi / 2, -np.pi / 2, -np.pi / 4, -3 * np.pi / 4, 0.0, -np.pi]))
    return ClaseSintetica(
        glosa=glosa, puntos=np.array([[0.55, -0.35], [0.55, -0.35]]), forma_ini=forma, forma_fin=None,
        angulo_ini=ang, angulo_fin=ang, dos_manos=0, cejas=0.0, boca=0.0, repeticiones=1, duracion_s=0.9,
    )


# ----------------------------------------------------------------------------
# Geometría
# ----------------------------------------------------------------------------


def _rot(v: np.ndarray, a: float) -> np.ndarray:
    c, s = np.cos(a), np.sin(a)
    return np.array([c * v[0] - s * v[1], s * v[0] + c * v[1]])


def mano(muneca: np.ndarray, tam: float, angulo: float, forma: np.ndarray, lado: float) -> np.ndarray:
    """21 puntos de una mano. ``lado`` = +1 mano derecha de la persona vista de frente, −1 izquierda."""
    u = np.array([np.cos(angulo), np.sin(angulo)])
    v = np.array([-u[1], u[0]]) * lado
    pts = np.zeros((21, 3))
    pts[0, :2] = muneca
    flex, sep = forma[:5], forma[5]
    bases = {1: (0.25, 0.32), 5: (0.9, 0.3), 9: (1.0, 0.05), 13: (0.95, -0.18), 17: (0.85, -0.38)}
    largos = {1: (0.35, 0.3, 0.25), 5: (0.45, 0.28, 0.22), 9: (0.5, 0.3, 0.23), 13: (0.46, 0.28, 0.22), 17: (0.36, 0.22, 0.2)}
    abanico = {1: 0.9, 5: 0.15, 9: 0.0, 13: -0.12, 17: -0.25}
    for dedo, (base, f) in enumerate(zip((1, 5, 9, 13, 17), flex)):
        a, b = bases[base]
        p = muneca + tam * (a * u + b * v)
        pts[base, :2] = p
        d = _rot(u, abanico[base] * (0.4 + sep) * lado)
        if base == 1:
            d = _rot(d, -f * 1.3 * lado)
        z = 0.0
        for j in range(3):
            curva = f * (j + 1) * 0.85
            paso = tam * largos[base][j]
            p = p + paso * np.cos(curva) * d
            z -= paso * np.sin(curva)
            pts[base + 1 + j, :2] = p
            pts[base + 1 + j, 2] = z
    return pts


def _cara(nariz: np.ndarray, tam: float, cejas: float, boca: float) -> np.ndarray:
    pts = np.zeros((len(F.CARA_INDICES_MEDIAPIPE), 3))
    for k, idx in enumerate(F.CARA_INDICES_MEDIAPIPE):
        dx, dy = _CARA_BASE[idx]
        if idx in _CEJAS:
            dy -= 0.07 * cejas
        if idx in _LABIO_INF:
            dy += 0.08 * boca
        pts[k, :2] = nariz + tam * np.array([dx, dy])
        pts[k, 2] = -0.02 * tam
    return pts


def _cuadro(f: Firmante, mano_d: np.ndarray | None, mano_i: np.ndarray | None, cejas: float, boca: float,
            mov_cabeza: np.ndarray) -> np.ndarray:
    """Arma un cuadro canónico (pose + manos + cara) en coordenadas de imagen."""
    w = f.ancho
    c = np.array([f.cx, f.cy])
    out = np.zeros((F.N_PUNTOS, 3), dtype=np.float32)
    hombro_der = c + np.array([-w / 2, 0.0])  # derecha de la persona = izquierda de la imagen
    hombro_izq = c + np.array([w / 2, 0.0])
    nariz = c + np.array([0.0, -0.62 * w]) + mov_cabeza
    out[F.IDX_NARIZ, :2] = nariz
    out[F.IDX_HOMBRO_IZQ, :2] = hombro_izq
    out[F.IDX_HOMBRO_DER, :2] = hombro_der

    for hombro, mano_pts, i_codo, i_mun, inicio, signo in (
        (hombro_der, mano_d, 4, 6, F.INICIO_MANO_DER, -1.0),
        (hombro_izq, mano_i, 3, 5, F.INICIO_MANO_IZQ, 1.0),
    ):
        mun = mano_pts[0, :2] if mano_pts is not None else hombro + np.array([0.15 * signo * w, 1.6 * w])
        medio = (hombro + mun) / 2
        codo = medio + np.array([0.35 * signo * w, 0.25 * w])
        out[F.INICIO_POSE + i_codo, :2] = codo
        out[F.INICIO_POSE + i_mun, :2] = mun
        if mano_pts is not None:
            out[inicio:inicio + 21] = mano_pts
    out[F.INICIO_CARA:] = _cara(nariz, f.tam_cara * w, cejas, boca)
    return out


def _visible(f: Firmante, cuadro: np.ndarray) -> np.ndarray:
    """Quita lo que queda fuera de la imagen (como haría el detector)."""
    for inicio in (F.INICIO_MANO_IZQ, F.INICIO_MANO_DER):
        mun = cuadro[inicio]
        if mun[1] > 1.0 or mun[1] < 0 or mun[0] < 0 or mun[0] > f.aspecto:
            cuadro[inicio:inicio + 21] = 0.0
    for i in range(F.INICIO_POSE, F.INICIO_MANO_IZQ):
        p = cuadro[i]
        if p[1] > 1.0 or p[1] < 0 or p[0] < 0 or p[0] > f.aspecto:
            cuadro[i] = 0.0
    return cuadro


def _a_imagen(f: Firmante, p: np.ndarray) -> np.ndarray:
    """De coordenadas corporales (anchos de hombro, x positivo hacia el lado dominante) a imagen."""
    x = -p[0] if not f.zurdo else p[0]
    return np.array([f.cx + x * f.ancho, f.cy + p[1] * f.ancho])


def _trayectoria(puntos: np.ndarray, s: np.ndarray, repeticiones: int) -> np.ndarray:
    """Interpola waypoints con ``s`` ∈ [0, 1]; con repeticiones hace idas y vueltas."""
    if repeticiones > 1:
        s = (1.0 - np.cos(np.pi * repeticiones * s)) / 2.0
    k = len(puntos) - 1
    pos = s * k
    i = np.clip(np.floor(pos).astype(int), 0, k - 1)
    t = (pos - i)[:, None]
    t = t * t * (3 - 2 * t)  # suavizado
    return puntos[i] * (1 - t) + puntos[i + 1] * t


def generar_muestra(
    rng: np.random.Generator,
    f: Firmante,
    clase: ClaseSintetica,
    fps: float | None = None,
    con_reposo: bool = False,
    sesion: str = "sintetica",
) -> Muestra:
    """Una ejecución de la seña. ``con_reposo`` agrega entrada/salida de las manos y reposo."""
    fps = fps or float(rng.choice([15.0, 20.0, 30.0]))
    dur = clase.duracion_s / f.velocidad * float(rng.uniform(0.85, 1.15))
    n = max(6, int(round(dur * fps)))
    s = np.linspace(0, 1, n)
    s = np.clip(s + rng.normal(0, 0.02, size=n).cumsum() * 0.02, 0, 1)  # ritmo irregular
    s.sort()

    reposo = np.array([0.15, 1.9])  # muñeca bajo el cuadro
    if clase.fondo:
        modo = int(rng.integers(0, 3))
        if modo == 0:  # rascarse la cara
            base = np.array([0.3, -0.75])
            tray = base + np.stack([0.05 * np.sin(s * 25), 0.04 * np.cos(s * 19)], 1)
        elif modo == 1:  # manos bajas acomodándose
            base = np.array([rng.uniform(-0.6, 0.6), rng.uniform(0.8, 1.2)])
            tray = base + rng.normal(0, 0.03, size=(n, 2)).cumsum(0) * 0.3
        else:  # mano que entra y sale sin señar
            tray = np.stack([np.full(n, rng.uniform(-0.4, 0.8)), 1.6 - 0.9 * np.sin(np.pi * s)], 1)
        formas = np.tile(np.array([0.3, 0.3, 0.3, 0.3, 0.3, 0.3]), (n, 1)) + rng.normal(0, 0.1, size=(n, 6))
        angulos = np.full(n, -np.pi / 2 + rng.uniform(-0.5, 0.5))
    else:
        jitter = rng.normal(0, 0.07, size=clase.puntos.shape)
        puntos = (clase.puntos + jitter) * f.amplitud
        tray = _trayectoria(puntos, s, clase.repeticiones)
        fin = clase.forma_fin if clase.forma_fin is not None else clase.forma_ini
        formas = clase.forma_ini[None] * (1 - s[:, None]) + fin[None] * s[:, None]
        formas = np.clip(formas + rng.normal(0, 0.06, size=6) + f.estilo.mean() * 0.2, 0, 1)
        angulos = clase.angulo_ini * (1 - s) + clase.angulo_fin * s + rng.normal(0, 0.12)

    cuadros = []
    lado_d = -1.0 if f.zurdo else 1.0
    tam = f.tam_mano * f.ancho

    def hacer(pos_d: np.ndarray, forma: np.ndarray, ang: float, pos_i: np.ndarray | None, cejas: float, boca: float):
        muneca_d = _a_imagen(f, pos_d)
        ang_img = ang if not f.zurdo else np.pi - ang
        md = mano(muneca_d, tam, ang_img, forma, lado_d)
        mi = None
        if pos_i is not None:
            muneca_i = _a_imagen(f, pos_i)
            ang_i = np.pi - ang_img
            mi = mano(muneca_i, tam, ang_i, forma, -lado_d)
        if f.zurdo:
            md, mi = mi, md
        cab = rng.normal(0, 0.004, size=2)
        return _visible(f, _cuadro(f, md, mi, cejas, boca, cab))

    def no_dominante(p: np.ndarray, k: float) -> np.ndarray | None:
        if clase.dos_manos == 1:
            return np.array([-p[0], p[1]]) + np.array([-0.05 * k, 0.0])
        if clase.dos_manos == 2:
            return np.array([-0.35, 0.45])
        return None

    # Entrada desde reposo
    if con_reposo:
        for _ in range(int(fps * rng.uniform(0.3, 0.6))):
            cuadros.append(_visible(f, _cuadro(f, None, None, 0.0, 0.0, rng.normal(0, 0.004, size=2))))
        n_sub = max(3, int(fps * 0.25))
        for k in np.linspace(0, 1, n_sub, endpoint=False):
            p = reposo * (1 - k) + tray[0] * k
            cuadros.append(hacer(p, formas[0], angulos[0], no_dominante(p, k), 0.0, 0.0))

    for i in range(n):
        intensidad = np.sin(np.pi * s[i]) if not clase.fondo else 0.0
        cuadros.append(
            hacer(tray[i], formas[i], angulos[i], no_dominante(tray[i], s[i]), clase.cejas * intensidad,
                  clase.boca * intensidad)
        )

    if con_reposo:
        n_sub = max(3, int(fps * 0.25))
        for k in np.linspace(0, 1, n_sub)[1:]:
            p = tray[-1] * (1 - k) + reposo * k
            cuadros.append(hacer(p, formas[-1], angulos[-1], no_dominante(p, 1 - k), 0.0, 0.0))
        for _ in range(int(fps * rng.uniform(0.4, 0.7))):
            cuadros.append(_visible(f, _cuadro(f, None, None, 0.0, 0.0, rng.normal(0, 0.004, size=2))))

    x = np.stack(cuadros).astype(np.float32)
    mascara = np.abs(x).sum(-1) > 0
    x += rng.normal(0, 0.0015, size=x.shape).astype(np.float32)
    # pérdidas ocasionales del detector de manos
    for inicio in (F.INICIO_MANO_IZQ, F.INICIO_MANO_DER):
        perdidas = rng.random(len(x)) < 0.03
        mascara[perdidas, inicio:inicio + 21] = False
    x[~mascara] = 0.0

    tiempos = (np.arange(len(x)) * 1000.0 / fps + rng.normal(0, 2.0, size=len(x))).astype(np.float32)
    tiempos = np.maximum.accumulate(tiempos)
    return Muestra(cuadros=x, tiempos_ms=tiempos, glosa=clase.glosa, firmante=f.id, sesion=sesion, origen="sintetico")


def generar_conjunto(
    n_clases: int = 20,
    n_firmantes: int = 12,
    repeticiones: int = 6,
    semilla: int = 0,
    con_fondo: bool = True,
) -> tuple[list[Muestra], list[str]]:
    """Conjunto de muestras ya segmentadas + lista de clases (``_FONDO`` primero si aplica)."""
    rng = np.random.default_rng(semilla)
    clases = [nueva_clase(rng, f"SENA-{k:02d}") for k in range(n_clases)]
    if con_fondo:
        clases.insert(0, clase_fondo())
    firmantes = [nuevo_firmante(rng, f"P{j:03d}") for j in range(n_firmantes)]
    muestras = []
    for f in firmantes:
        for c in clases:
            for _ in range(repeticiones):
                muestras.append(generar_muestra(rng, f, c))
    return muestras, [c.glosa for c in clases]


def generar_letras(n_letras: int = 10, n_firmantes: int = 8, repeticiones: int = 4, semilla: int = 1):
    rng = np.random.default_rng(semilla)
    letras = [nueva_letra(rng, f"LETRA-{chr(65 + k)}") for k in range(n_letras)]
    firmantes = [nuevo_firmante(rng, f"P{j:03d}") for j in range(n_firmantes)]
    muestras = [generar_muestra(rng, f, c) for f in firmantes for c in letras for _ in range(repeticiones)]
    return muestras, [c.glosa for c in letras]
