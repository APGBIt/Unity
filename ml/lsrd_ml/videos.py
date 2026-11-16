"""Canal de videos: de un catálogo de fuentes con permiso a muestras de entrenamiento.

Pasos (cada uno deja un CSV que una persona puede revisar y corregir):

1. ``leer_fuentes`` + ``utilizables`` — qué fuentes tienen el permiso resuelto.
2. ``descargar_fuente`` / ``indexar`` — ``inventario.csv`` con un renglón por video.
3. ``proponer_anotaciones`` — corta cada video en señas con el mismo segmentador
   de la app y sugiere la glosa a partir del título. Sale ``anotaciones.csv``.
4. ``generar_muestras`` — convierte las anotaciones revisadas en ``*.npz``.
"""

from __future__ import annotations

import csv
import hashlib
import json
import re
import subprocess
import sys
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Optional

import numpy as np

from . import segmentacion as G
from .vocabulario import Entrada

PERMISOS_VALIDOS = ("pendiente", "autorizado", "licencia_abierta", "propio")
EXTENSIONES_VIDEO = (".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v")
CAMPOS_INVENTARIO = ["archivo", "fuente", "titulo", "url", "sha256", "firmante", "permiso", "uso"]
CAMPOS_ANOTACION = ["archivo", "inicio_ms", "fin_ms", "glosa", "firmante", "estado", "sugerencia", "notas"]

Extraer = Callable[[Path], tuple[np.ndarray, np.ndarray]]


@dataclass(frozen=True)
class Fuente:
    id: str
    nombre: str
    url: str
    tipo: str
    titular: str
    permiso: str
    evidencia_permiso: str
    uso: tuple[str, ...]
    firmante: str = ""

    def motivo_bloqueo(self, uso: str = "entrenamiento") -> str:
        """Cadena vacía si la fuente se puede usar; si no, la razón."""
        if self.permiso not in PERMISOS_VALIDOS:
            return f"permiso desconocido «{self.permiso}»"
        if self.permiso == "pendiente":
            return "permiso pendiente"
        if not self.evidencia_permiso.strip():
            return "falta la evidencia del permiso"
        if uso not in self.uso:
            return f"el permiso no cubre el uso «{uso}»"
        if not self.url.strip():
            return "falta la dirección o carpeta"
        return ""


def leer_fuentes(ruta: Path) -> list[Fuente]:
    with open(ruta, encoding="utf-8", newline="") as fh:
        filas = list(csv.DictReader(fh))
    return [
        Fuente(
            id=f["id"].strip(), nombre=f["nombre"].strip(), url=f["url"].strip(), tipo=f["tipo"].strip(),
            titular=f["titular"].strip(), permiso=f["permiso"].strip(),
            evidencia_permiso=f.get("evidencia_permiso", "").strip(),
            uso=tuple(u.strip() for u in f.get("uso", "").split("|") if u.strip()),
            firmante=f.get("firmante", "").strip(),
        )
        for f in filas if f.get("id", "").strip()
    ]


def utilizables(fuentes: Iterable[Fuente], uso: str = "entrenamiento") -> tuple[list[Fuente], dict[str, str]]:
    ok, bloqueadas = [], {}
    for f in fuentes:
        motivo = f.motivo_bloqueo(uso)
        if motivo:
            bloqueadas[f.id] = motivo
        else:
            ok.append(f)
    return ok, bloqueadas


# ----------------------------------------------------------------------------
# Descarga e inventario
# ----------------------------------------------------------------------------


def descargar_fuente(fuente: Fuente, carpeta: Path, ejecutar=subprocess.run) -> Path:
    """Descarga una fuente en línea con yt-dlp a ``carpeta/<id>/``. Devuelve esa carpeta."""
    motivo = fuente.motivo_bloqueo()
    if motivo:
        raise PermissionError(f"La fuente «{fuente.id}» no se puede descargar: {motivo}")
    destino = Path(carpeta) / fuente.id
    destino.mkdir(parents=True, exist_ok=True)
    orden = [
        sys.executable, "-m", "yt_dlp", "--no-overwrites", "--write-info-json", "--restrict-filenames",
        "-f", "mp4[height<=720]/best[height<=720]/best", "-o", str(destino / "%(id)s.%(ext)s"), fuente.url,
    ]
    ejecutar(orden, check=True)
    return destino


def _sha256(ruta: Path) -> str:
    h = hashlib.sha256()
    with open(ruta, "rb") as fh:
        for bloque in iter(lambda: fh.read(1 << 20), b""):
            h.update(bloque)
    return h.hexdigest()


def indexar(fuente: Fuente, carpeta_fuente: Path, raiz: Path) -> list[dict]:
    """Un renglón de inventario por video. El firmante sale de la fuente, de la
    subcarpeta (``P001/…``) o queda como ``<fuente>-desconocido``."""
    filas = []
    carpeta_fuente = Path(carpeta_fuente)
    for ruta in sorted(carpeta_fuente.rglob("*")):
        if ruta.suffix.lower() not in EXTENSIONES_VIDEO:
            continue
        titulo, url = ruta.stem, ""
        info = ruta.with_suffix(".info.json")
        if info.exists():
            try:
                d = json.loads(info.read_text(encoding="utf-8"))
                titulo, url = d.get("title") or titulo, d.get("webpage_url") or ""
            except json.JSONDecodeError:
                pass
        relativo = ruta.relative_to(carpeta_fuente)
        firmante = fuente.firmante or (relativo.parts[0] if len(relativo.parts) > 1 else f"{fuente.id}-desconocido")
        filas.append({
            "archivo": str(ruta.relative_to(raiz)) if ruta.is_relative_to(raiz) else str(ruta),
            "fuente": fuente.id, "titulo": titulo, "url": url, "sha256": _sha256(ruta),
            "firmante": firmante, "permiso": fuente.permiso, "uso": "|".join(fuente.uso),
        })
    return filas


def escribir_csv(ruta: Path, campos: list[str], filas: list[dict]) -> None:
    Path(ruta).parent.mkdir(parents=True, exist_ok=True)
    with open(ruta, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=campos)
        w.writeheader()
        w.writerows(filas)


def leer_csv(ruta: Path) -> list[dict]:
    with open(ruta, encoding="utf-8", newline="") as fh:
        return list(csv.DictReader(fh))


# ----------------------------------------------------------------------------
# Del título a la glosa
# ----------------------------------------------------------------------------

_RUIDO = re.compile(r"\b(lsrd|lengua de señas dominicana|lengua de señas|en señas|seña de|señas?|como se dice|"
                    r"diccionario|oficial|video|rd)\b")


def normalizar(texto: str) -> str:
    """Minúsculas, sin tildes (conserva la ñ), sin signos."""
    t = texto.lower().replace("ñ", "\0")
    t = "".join(c for c in unicodedata.normalize("NFD", t) if unicodedata.category(c) != "Mn").replace("\0", "ñ")
    t = re.sub(r"[^a-z0-9ñ ]+", " ", t)
    return re.sub(r"\s+", " ", t).strip()


def indice_de_terminos(entradas: list[Entrada]) -> dict[str, str]:
    indice: dict[str, str] = {}
    for e in entradas:
        if e.tipo == "fondo":
            continue
        for termino in (e.espanol, e.glosa.replace("-", " "), *e.sinonimos):
            indice.setdefault(normalizar(termino), e.glosa)
    return indice


def glosa_de_titulo(titulo: str, indice: dict[str, str]) -> str:
    """Glosa del vocabulario que corresponde al título de un video, o ``""``."""
    t = normalizar(titulo)
    if t in indice:
        return indice[t]
    limpio = re.sub(r"\s+", " ", _RUIDO.sub(" ", t)).strip()
    return indice.get(limpio, "")


# ----------------------------------------------------------------------------
# Anotaciones
# ----------------------------------------------------------------------------


def ruta_landmarks(carpeta_cache: Path, archivo: str) -> Path:
    return Path(carpeta_cache) / (re.sub(r"[^A-Za-z0-9_.-]+", "_", archivo) + ".npz")


def landmarks_de_video(archivo: str, raiz: Path, carpeta_cache: Path, extraer: Extraer) -> tuple[np.ndarray, np.ndarray]:
    """Puntos clave del video completo, con caché en disco (la extracción es lo lento)."""
    cache = ruta_landmarks(carpeta_cache, archivo)
    if cache.exists():
        z = np.load(cache, allow_pickle=False)
        return z["cuadros"], z["tiempos_ms"]
    cuadros, tiempos = extraer(Path(raiz) / archivo)
    cache.parent.mkdir(parents=True, exist_ok=True)
    np.savez_compressed(cache, cuadros=cuadros.astype(np.float32), tiempos_ms=tiempos.astype(np.float32))
    return cuadros, tiempos


def proponer_anotaciones(
    inventario: list[dict], raiz: Path, carpeta_cache: Path, extraer: Extraer, entradas: list[Entrada],
    existentes: Optional[list[dict]] = None,
) -> list[dict]:
    """Segmenta cada video y sugiere glosas. Conserva las anotaciones que ya
    existían para un video (no pisa el trabajo de revisión)."""
    indice = indice_de_terminos(entradas)
    existentes = existentes or []
    ya = {a["archivo"] for a in existentes}
    filas = list(existentes)
    for v in inventario:
        if v["archivo"] in ya:
            continue
        cuadros, tiempos = landmarks_de_video(v["archivo"], raiz, carpeta_cache, extraer)
        glosa = glosa_de_titulo(v.get("titulo", ""), indice)
        segmentos = G.segmentar_secuencia(cuadros, tiempos) if len(cuadros) else []
        if not segmentos:
            filas.append({"archivo": v["archivo"], "inicio_ms": "", "fin_ms": "", "glosa": glosa,
                          "firmante": v.get("firmante", ""), "estado": "sin_senas", "sugerencia": "",
                          "notas": "No se detectó ninguna seña: revisar encuadre o anotar los tiempos a mano."})
            continue
        for ini, fin in segmentos:
            filas.append({"archivo": v["archivo"], "inicio_ms": f"{ini:.0f}", "fin_ms": f"{fin:.0f}", "glosa": glosa,
                          "firmante": v.get("firmante", ""), "estado": "propuesta",
                          "sugerencia": "titulo" if glosa else "", "notas": ""})
    return filas


def generar_muestras(
    anotaciones: list[dict], carpeta_cache: Path, salida: Path, glosas_validas: set[str],
    incluir_propuestas: bool = False,
) -> dict:
    """Escribe un ``.npz`` por anotación revisada. Devuelve un resumen."""
    salida = Path(salida)
    salida.mkdir(parents=True, exist_ok=True)
    estados = {"revisada"} | ({"propuesta"} if incluir_propuestas else set())
    resumen = {"escritas": 0, "omitidas": {}, "por_glosa": {}, "firmantes": set()}

    def omitir(razon: str) -> None:
        resumen["omitidas"][razon] = resumen["omitidas"].get(razon, 0) + 1

    for k, a in enumerate(anotaciones):
        if a.get("estado") not in estados:
            omitir(f"estado {a.get('estado') or 'vacío'}")
            continue
        glosa = a.get("glosa", "").strip()
        if not glosa:
            omitir("sin glosa")
            continue
        if glosa not in glosas_validas:
            omitir("glosa fuera del vocabulario")
            continue
        if not a.get("firmante", "").strip():
            omitir("sin firmante")
            continue
        cache = ruta_landmarks(carpeta_cache, a["archivo"])
        if not cache.exists():
            omitir("sin puntos clave extraídos")
            continue
        z = np.load(cache, allow_pickle=False)
        tiempos = z["tiempos_ms"]
        sel = (tiempos >= float(a["inicio_ms"])) & (tiempos <= float(a["fin_ms"]))
        if int(sel.sum()) < 4:
            omitir("segmento demasiado corto")
            continue
        nombre = f"{a['firmante']}__{glosa}__{Path(a['archivo']).stem}_{k:05d}.npz"
        np.savez_compressed(salida / nombre, cuadros=z["cuadros"][sel].astype(np.float32),
                            tiempos_ms=tiempos[sel].astype(np.float32), glosa=glosa, firmante=a["firmante"],
                            sesion=Path(a["archivo"]).stem)
        resumen["escritas"] += 1
        resumen["por_glosa"][glosa] = resumen["por_glosa"].get(glosa, 0) + 1
        resumen["firmantes"].add(a["firmante"])
    resumen["firmantes"] = sorted(resumen["firmantes"])
    return resumen
