"""Memoria de traducción: frases validadas por intérpretes y frases rápidas."""

from __future__ import annotations

import csv
from dataclasses import dataclass
from difflib import SequenceMatcher
from pathlib import Path

from .vocabulario import normalizar

UMBRAL_SIMILITUD = 0.92


@dataclass(frozen=True)
class Frase:
    id: str
    espanol: str
    glosas: tuple[str, ...]
    direccion: str
    rapida: bool
    estado: str
    contexto: str

    @property
    def validada(self) -> bool:
        return self.estado == "validada" and bool(self.glosas)

    def a_dict(self) -> dict:
        return {
            "id": self.id, "espanol": self.espanol, "glosas": list(self.glosas), "direccion": self.direccion,
            "rapida": self.rapida, "estado": self.estado, "validada": self.validada, "contexto": self.contexto,
        }


class Memoria:
    def __init__(self, ruta: Path) -> None:
        with open(ruta, encoding="utf-8", newline="") as fh:
            filas = list(csv.DictReader(fh))
        self.frases = [
            Frase(
                id=f["id"].strip(),
                espanol=f["espanol"].strip(),
                glosas=tuple(g for g in f["glosas"].split() if g),
                direccion=f["direccion"].strip(),
                rapida=f["rapida"].strip() == "1",
                estado=f["estado"].strip(),
                contexto=f.get("contexto", "").strip(),
            )
            for f in filas
        ]
        self.validadas = [f for f in self.frases if f.validada]
        self._por_texto = {normalizar(f.espanol): f for f in self.validadas}
        self._por_glosas = {f.glosas: f for f in self.validadas}

    def buscar_texto(self, texto: str) -> tuple[Frase | None, float]:
        clave = normalizar(texto)
        if clave in self._por_texto:
            return self._por_texto[clave], 1.0
        mejor, puntaje = None, 0.0
        for k, f in self._por_texto.items():
            s = SequenceMatcher(None, clave, k).ratio()
            if s > puntaje:
                mejor, puntaje = f, s
        if puntaje >= UMBRAL_SIMILITUD:
            return mejor, puntaje
        return None, puntaje

    def buscar_glosas(self, glosas: list[str]) -> Frase | None:
        return self._por_glosas.get(tuple(glosas))

    def ejemplos(self, limite: int = 25) -> list[Frase]:
        return self.validadas[:limite]

    def rapidas(self, direccion: str | None = None) -> list[Frase]:
        return [f for f in self.frases if f.rapida and (direccion is None or f.direccion == direccion)]
