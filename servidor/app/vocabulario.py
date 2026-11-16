"""Vocabulario cerrado del MVP y normalización de texto en español."""

from __future__ import annotations

import csv
import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path

PREGUNTAS = {"QUE", "DONDE", "CUANDO", "CUANTO", "QUIEN", "COMO", "POR-QUE", "CUAL"}

# Palabras que el español necesita pero la LSRD expresa con el espacio, el orden
# o la cara; en la traducción literal se omiten.
FUNCIONALES = {
    "el", "la", "los", "las", "un", "una", "unos", "unas", "lo", "de", "del", "al", "a", "en", "y", "e", "o", "u",
    "que", "se", "le", "les", "me", "te", "nos", "su", "sus", "mi", "mis", "tu", "tus", "por", "para", "con", "sin",
    "es", "son", "era", "fue", "está", "están", "esta", "este", "estos", "estas", "ese", "esa", "eso", "muy", "como",
    "si", "ya", "pero", "porque", "cuando", "donde", "hay", "ser", "estar", "ha", "he", "han", "hemos", "va", "tengo que",
    "tiene que", "debe", "debo", "deben", "usted", "ustedes",
}

_PUNTUACION = re.compile(r"[¿?¡!.,;:\"“”«»()\[\]…]")


def normalizar(texto: str) -> str:
    """Minúsculas, sin puntuación, espacios simples. Conserva tildes y ñ."""
    t = unicodedata.normalize("NFC", texto).lower()
    t = _PUNTUACION.sub(" ", t)
    return " ".join(t.split())


def sin_tildes(texto: str) -> str:
    descompuesto = unicodedata.normalize("NFD", texto.replace("ñ", "\0"))
    limpio = "".join(c for c in descompuesto if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", limpio).replace("\0", "ñ")


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

    def a_dict(self) -> dict:
        return {
            "glosa": self.glosa, "espanol": self.espanol, "sinonimos": list(self.sinonimos),
            "categoria": self.categoria, "prioridad": self.prioridad, "tipo": self.tipo,
            "modelo": self.modelo, "estado": self.estado,
        }


class Vocabulario:
    def __init__(self, ruta: Path) -> None:
        with open(ruta, encoding="utf-8", newline="") as fh:
            filas = list(csv.DictReader(fh))
        self.entradas = [
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
        self.por_glosa = {e.glosa: e for e in self.entradas}
        # Índice de frases en español → glosa (con y sin tildes)
        self._indice: dict[str, str] = {}
        self._indice_sin_tildes: dict[str, str] = {}
        for e in self.entradas:
            if e.tipo in ("fondo", "letra"):
                continue
            for s in (e.espanol,) + e.sinonimos:
                clave = normalizar(s)
                if clave and not clave.startswith("("):
                    self._indice.setdefault(clave, e.glosa)
                    self._indice_sin_tildes.setdefault(sin_tildes(clave), e.glosa)
        self.max_palabras = max(len(k.split()) for k in self._indice)
        self.letras = {e.espanol: e.glosa for e in self.entradas if e.tipo == "letra"}
        self.numeros = {e.espanol: e.glosa for e in self.entradas if e.tipo == "numero"}

    @property
    def glosas_senas(self) -> list[str]:
        return [e.glosa for e in self.entradas if e.tipo in ("seña", "numero")]

    def existe(self, glosa: str) -> bool:
        return glosa in self.por_glosa and self.por_glosa[glosa].tipo != "fondo"

    def buscar(self, frase_normalizada: str) -> str | None:
        """Glosa para una frase normalizada (primero con tildes; sin tildes solo si no es funcional)."""
        if frase_normalizada in self._indice:
            return self._indice[frase_normalizada]
        if frase_normalizada in FUNCIONALES:
            return None
        return self._indice_sin_tildes.get(sin_tildes(frase_normalizada))

    def espanol_de(self, glosa: str) -> str:
        if glosa.startswith("DACT:"):
            return glosa[5:]
        e = self.por_glosa.get(glosa)
        if e is None:
            return glosa.lower().replace("-", " ")
        return e.espanol.split(" (")[0]

    def deletreo(self, palabra: str) -> list[str]:
        """Glosas de letras para deletrear una palabra (se omiten caracteres sin letra)."""
        salida = []
        for c in sin_tildes(palabra.lower()):
            if c in self.letras:
                salida.append(self.letras[c])
            elif c.isdigit() and c in self.numeros:
                salida.append(self.numeros[c])
        return salida
