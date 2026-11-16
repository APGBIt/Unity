"""Remuestreo temporal a longitud fija por vecino más cercano.

Se usa vecino más cercano (no interpolación) porque es robusto a puntos
ausentes y se reproduce exactamente en C# (``Remuestreo.cs``): con T = 32,
``(i + 0.5) * L / T`` es exacto en coma flotante.
"""

from __future__ import annotations

import math

import numpy as np

from . import formato as F


def indices(longitud: int, t: int = F.LONGITUD_SECUENCIA) -> list[int]:
    if longitud <= 0:
        raise ValueError("La secuencia está vacía")
    return [min(longitud - 1, int(math.floor((i + 0.5) * longitud / t))) for i in range(t)]


def remuestrear(cuadros: np.ndarray, t: int = F.LONGITUD_SECUENCIA) -> np.ndarray:
    """``[L, N, 3]`` → ``[t, N, 3]``."""
    return cuadros[indices(len(cuadros), t)]
