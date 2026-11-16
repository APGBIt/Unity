"""Aumentos de datos para entrenamiento (solo se aplican al entrenar).

Simulan lo que cambia entre personas y dispositivos: velocidad de la seña,
encuadre, ángulo de cámara, mano dominante, pérdidas del detector y ruido.
Los puntos ausentes siguen valiendo exactamente 0 después de cada aumento.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from . import formato as F


@dataclass
class ConfigAumentos:
    prob_espejo: float = 0.5
    recorte_temporal: float = 0.12  # fracción máxima recortada en cada extremo
    prob_quitar_cuadros: float = 0.5
    max_cuadros_quitados: float = 0.15
    rotacion_grados: float = 10.0
    escala: tuple[float, float] = (0.85, 1.15)
    escala_aspecto: tuple[float, float] = (0.92, 1.08)
    desplazamiento: float = 0.08
    ruido: float = 0.003
    prob_perder_mano: float = 0.08
    prob_perder_cara: float = 0.1


def _mascara(cuadros: np.ndarray) -> np.ndarray:
    return np.abs(cuadros).sum(-1) > 0


def espejo(cuadros: np.ndarray) -> np.ndarray:
    """Espejo horizontal: invierte x y permuta izquierda ↔ derecha."""
    salida = cuadros[:, list(F.PERMUTACION_ESPEJO), :].copy()
    mascara = _mascara(salida)
    salida[..., 0] = np.where(mascara, -salida[..., 0], 0.0)
    return salida


def aumentar_secuencia(cuadros: np.ndarray, rng: np.random.Generator, cfg: ConfigAumentos | None = None) -> np.ndarray:
    cfg = cfg or ConfigAumentos()
    x = cuadros.astype(np.float32, copy=True)
    largo = len(x)

    # Recorte temporal (simula segmentaciones imperfectas)
    if largo > 8 and cfg.recorte_temporal > 0:
        max_r = int(largo * cfg.recorte_temporal)
        ini = rng.integers(0, max_r + 1)
        fin = largo - rng.integers(0, max_r + 1)
        if fin - ini >= 6:
            x = x[ini:fin]

    # Quitar cuadros (simula dispositivos lentos o caídas de cuadro)
    if len(x) > 8 and rng.random() < cfg.prob_quitar_cuadros:
        k = int(len(x) * rng.uniform(0, cfg.max_cuadros_quitados))
        if k > 0:
            quitar = rng.choice(len(x), size=k, replace=False)
            x = np.delete(x, quitar, axis=0)

    mascara = _mascara(x)

    if rng.random() < cfg.prob_espejo:
        x = espejo(x)
        mascara = _mascara(x)

    # Transformación afín 2D alrededor del centro de los puntos visibles
    if mascara.any():
        centro = x[mascara][:, :2].mean(0)
    else:
        centro = np.zeros(2, dtype=np.float32)
    ang = np.deg2rad(rng.uniform(-cfg.rotacion_grados, cfg.rotacion_grados))
    s = rng.uniform(*cfg.escala)
    sa = rng.uniform(*cfg.escala_aspecto)
    rot = np.array([[np.cos(ang), -np.sin(ang)], [np.sin(ang), np.cos(ang)]], dtype=np.float32)
    m = rot @ np.diag([s * sa, s / sa]).astype(np.float32)
    desp = rng.uniform(-cfg.desplazamiento, cfg.desplazamiento, size=2).astype(np.float32)
    xy = (x[..., :2] - centro) @ m.T + centro + desp
    x[..., :2] = xy
    x[..., 2] *= s

    # Ruido en los puntos
    x += rng.normal(0.0, cfg.ruido, size=x.shape).astype(np.float32)

    # Pérdidas del detector
    for inicio, fin in (F.GRUPOS["mano_izq"], F.GRUPOS["mano_der"]):
        if rng.random() < cfg.prob_perder_mano and len(x) > 4:
            a = rng.integers(0, len(x))
            b = min(len(x), a + rng.integers(1, max(2, len(x) // 4)))
            mascara[a:b, inicio:fin] = False
    if rng.random() < cfg.prob_perder_cara:
        i, f = F.GRUPOS["cara"]
        mascara[:, i:f] = False

    x[~mascara] = 0.0
    return x


def aumentar_mano(mano: np.ndarray, rng: np.random.Generator) -> np.ndarray:
    """Aumentos para una mano aislada ``[21, 3]`` (dactilología)."""
    x = mano.astype(np.float32, copy=True)
    centro = x[F.MANO_MUNECA, :2].copy()
    ang = np.deg2rad(rng.uniform(-12, 12))
    s = rng.uniform(0.85, 1.15)
    rot = np.array([[np.cos(ang), -np.sin(ang)], [np.sin(ang), np.cos(ang)]], dtype=np.float32)
    m = rot @ np.diag([s * rng.uniform(0.93, 1.07), s]).astype(np.float32)
    x[:, :2] = (x[:, :2] - centro) @ m.T + centro
    x += rng.normal(0.0, 0.002, size=x.shape).astype(np.float32)
    return x
