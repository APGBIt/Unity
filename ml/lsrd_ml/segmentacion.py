"""Segmentación de señas en tiempo real (detecta dónde empieza y termina cada seña).

La app Unity implementa exactamente la misma lógica en ``Segmentador.cs``. Las
pruebas de paridad comparan ambas implementaciones con los mismos datos, así que
cualquier cambio aquí debe reflejarse allá (y viceversa).

Reglas:

* Un cuadro está **activo** si hay al menos una mano visible por encima de la
  línea de reposo (hombros + ``linea_reposo`` anchos de hombro hacia abajo).
  Sin hombros visibles, basta con que haya una mano.
* Una seña **empieza** tras ``inicio_ms`` de actividad continua.
* Una seña **termina** cuando las manos bajan o salen del cuadro durante
  ``fin_ms``, cuando las muñecas se quedan quietas ``quietud_ms`` (señas
  estáticas como números o letras), o al llegar a ``max_ms``.
* Los segmentos más cortos que ``min_ms`` se descartan.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Optional

import numpy as np

from . import formato as F

REPOSO, POSIBLE, SENA, ESPERA = 0, 1, 2, 3
INFINITO = math.inf


@dataclass
class ParametrosSegmentacion:
    inicio_ms: float = 100.0
    fin_ms: float = 250.0
    min_ms: float = 250.0
    max_ms: float = 3000.0
    quietud_ms: float = 400.0
    umbral_velocidad: float = 0.5  # anchos de hombro por segundo
    linea_reposo: float = 1.5  # anchos de hombro por debajo de la línea de hombros
    usar_quietud: bool = True
    factor_mano_a_hombros: float = 3.5  # sin hombros: ancho ≈ 3.5 × tamaño de la mano


def _presente(cuadro: np.ndarray, idx: int) -> bool:
    p = cuadro[idx]
    return (abs(float(p[0])) + abs(float(p[1])) + abs(float(p[2]))) > 0.0


def _dist2d(a, b) -> float:
    dx = float(a[0]) - float(b[0])
    dy = float(a[1]) - float(b[1])
    return math.sqrt(dx * dx + dy * dy)


def ancho_referencia(cuadro: np.ndarray, p: ParametrosSegmentacion) -> float:
    """Ancho de hombros (o su estimación a partir de la mano). 0 si no hay referencia."""
    if _presente(cuadro, F.IDX_HOMBRO_IZQ) and _presente(cuadro, F.IDX_HOMBRO_DER):
        return _dist2d(cuadro[F.IDX_HOMBRO_IZQ], cuadro[F.IDX_HOMBRO_DER])
    for inicio in (F.INICIO_MANO_IZQ, F.INICIO_MANO_DER):
        if _presente(cuadro, inicio + F.MANO_MUNECA) and _presente(cuadro, inicio + F.MANO_BASE_MEDIO):
            return p.factor_mano_a_hombros * _dist2d(cuadro[inicio + F.MANO_MUNECA], cuadro[inicio + F.MANO_BASE_MEDIO])
    return 0.0


def medir_cuadro(
    actual: np.ndarray,
    anterior: Optional[np.ndarray],
    dt_s: float,
    p: ParametrosSegmentacion,
) -> tuple[bool, float]:
    """Devuelve ``(activo, velocidad)``; velocidad en anchos de hombro por segundo."""
    munecas = (F.IDX_MUNECA_IZQ, F.IDX_MUNECA_DER)
    presentes = [m for m in munecas if _presente(actual, m)]
    if not presentes:
        return False, 0.0

    hombros = _presente(actual, F.IDX_HOMBRO_IZQ) and _presente(actual, F.IDX_HOMBRO_DER)
    activo = False
    if hombros:
        ancho = _dist2d(actual[F.IDX_HOMBRO_IZQ], actual[F.IDX_HOMBRO_DER])
        y_hombros = (float(actual[F.IDX_HOMBRO_IZQ][1]) + float(actual[F.IDX_HOMBRO_DER][1])) / 2.0
        for m in presentes:
            if ancho < 1e-6 or float(actual[m][1]) < y_hombros + p.linea_reposo * ancho:
                activo = True
    else:
        activo = True

    velocidad = INFINITO
    escala = ancho_referencia(actual, p)
    if anterior is not None and dt_s > 0.0 and escala > 1e-6:
        vmax = -1.0
        for m in munecas:
            if _presente(actual, m) and _presente(anterior, m):
                v = _dist2d(actual[m], anterior[m]) / escala / dt_s
                if v > vmax:
                    vmax = v
        if vmax >= 0.0:
            velocidad = vmax
    return activo, velocidad


class Segmentador:
    """Máquina de estados que emite segmentos ``(t_inicio_ms, t_fin_ms)``."""

    def __init__(self, parametros: Optional[ParametrosSegmentacion] = None):
        self.p = parametros or ParametrosSegmentacion()
        self.reiniciar()

    def reiniciar(self) -> None:
        self.estado = REPOSO
        self.t_inicio = 0.0
        self.t_ultimo_activo = 0.0
        self.t_quieto: Optional[float] = None
        self.t_inactivo: Optional[float] = None

    def _emitir(self, t_ini: float, t_fin: float) -> Optional[tuple[float, float]]:
        if t_fin - t_ini >= self.p.min_ms:
            return (t_ini, t_fin)
        return None

    def procesar(self, t_ms: float, activo: bool, velocidad: float) -> Optional[tuple[float, float]]:
        p = self.p
        if self.estado == REPOSO:
            if activo:
                self.estado = POSIBLE
                self.t_inicio = t_ms
            return None

        if self.estado == POSIBLE:
            if not activo:
                self.estado = REPOSO
                return None
            if t_ms - self.t_inicio >= p.inicio_ms:
                self.estado = SENA
                self.t_ultimo_activo = t_ms
                self.t_quieto = None
            return None

        if self.estado == SENA:
            if activo:
                self.t_ultimo_activo = t_ms
                if p.usar_quietud and velocidad < p.umbral_velocidad:
                    if self.t_quieto is None:
                        self.t_quieto = t_ms
                    elif t_ms - self.t_quieto >= p.quietud_ms and t_ms - self.t_inicio >= p.min_ms:
                        seg = self._emitir(self.t_inicio, t_ms)
                        self.estado = ESPERA
                        self.t_inactivo = None
                        return seg
                else:
                    self.t_quieto = None
                if t_ms - self.t_inicio >= p.max_ms:
                    seg = self._emitir(self.t_inicio, t_ms)
                    self.t_inicio = t_ms
                    self.t_quieto = None
                    return seg
                return None
            if t_ms - self.t_ultimo_activo >= p.fin_ms:
                self.estado = REPOSO
                return self._emitir(self.t_inicio, self.t_ultimo_activo)
            return None

        # ESPERA: tras una seña estática, hay que moverse o bajar las manos
        if activo:
            self.t_inactivo = None
            if velocidad >= p.umbral_velocidad:
                self.estado = POSIBLE
                self.t_inicio = t_ms
        else:
            if self.t_inactivo is None:
                self.t_inactivo = t_ms
            elif t_ms - self.t_inactivo >= p.fin_ms:
                self.estado = REPOSO
        return None

    def cerrar(self) -> Optional[tuple[float, float]]:
        """Fin del flujo: emite la seña en curso, si la hay."""
        seg = None
        if self.estado == SENA:
            seg = self._emitir(self.t_inicio, self.t_ultimo_activo)
        self.reiniciar()
        return seg


def segmentar_secuencia(
    cuadros: np.ndarray,
    tiempos_ms: np.ndarray,
    parametros: Optional[ParametrosSegmentacion] = None,
) -> list[tuple[float, float]]:
    """Aplica el segmentador a una secuencia completa ``[L, N, 3]``."""
    p = parametros or ParametrosSegmentacion()
    seg = Segmentador(p)
    salida: list[tuple[float, float]] = []
    anterior = None
    t_ant = None
    for cuadro, t in zip(cuadros, tiempos_ms):
        dt = (float(t) - float(t_ant)) / 1000.0 if t_ant is not None else 0.0
        activo, vel = medir_cuadro(cuadro, anterior, dt, p)
        r = seg.procesar(float(t), activo, vel)
        if r is not None:
            salida.append(r)
        anterior, t_ant = cuadro, t
    r = seg.cerrar()
    if r is not None:
        salida.append(r)
    return salida


def recortar(cuadros: np.ndarray, tiempos_ms: np.ndarray, t_ini: float, t_fin: float) -> np.ndarray:
    """Cuadros cuyo tiempo cae dentro de ``[t_ini, t_fin]``."""
    sel = (tiempos_ms >= t_ini) & (tiempos_ms <= t_fin)
    return cuadros[sel]
