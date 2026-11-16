"""Lectura del vocabulario del MVP (``vocabulario/vocabulario_mvp.csv``)."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from pathlib import Path

RUTA_POR_DEFECTO = Path(__file__).resolve().parents[2] / "vocabulario" / "vocabulario_mvp.csv"


@dataclass(frozen=True)
class Entrada:
    glosa: str
    espanol: str
    sinonimos: tuple[str, ...]
    categoria: str
    prioridad: int
    tipo: str
    modelo: str
    estado: str


def cargar(ruta: Path = RUTA_POR_DEFECTO) -> list[Entrada]:
    with open(ruta, encoding="utf-8", newline="") as fh:
        filas = list(csv.DictReader(fh))
    return [
        Entrada(
            glosa=f["glosa"].strip(),
            espanol=f["espanol"].strip(),
            sinonimos=tuple(s.strip() for s in f["sinonimos"].split("|") if s.strip()),
            categoria=f["categoria"].strip(),
            prioridad=int(f["prioridad"]),
            tipo=f["tipo"].strip(),
            modelo=f["modelo"].strip(),
            estado=f["estado"].strip(),
        )
        for f in filas
    ]


def clases_senas(entradas: list[Entrada], prioridad_max: int = 2, solo_validadas: bool = False) -> list[str]:
    """Clases del modelo de señas; ``_FONDO`` siempre va primero."""
    clases = [
        e.glosa for e in entradas
        if e.modelo == "senas" and e.tipo != "fondo" and e.prioridad <= prioridad_max
        and (not solo_validadas or e.estado == "validada")
    ]
    return ["_FONDO"] + clases


def clases_dactilologia(entradas: list[Entrada]) -> list[str]:
    return [e.glosa for e in entradas if e.modelo == "dactilologia"]
