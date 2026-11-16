"""Carga de muestras, divisiones por firmante y datasets de PyTorch.

Fuentes de datos:

* **Muestras de la app** (``*.json``): las graba el modo recolección de la app
  Unity con el mismo detector que usa el reconocimiento, así que no hay
  diferencia entre entrenamiento y uso real. Es la fuente principal.
* **Videos extraídos** (``*.npz``): resultado de ``scripts/extraer_videos.py``.

La evaluación siempre se hace con **firmantes que no estuvieron en el
entrenamiento**; de lo contrario la exactitud reportada es engañosa.
"""

from __future__ import annotations

import base64
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Optional

import numpy as np

from . import formato as F
from .aumentos import ConfigAumentos, aumentar_mano, aumentar_secuencia
from .remuestreo import remuestrear


@dataclass
class Muestra:
    cuadros: np.ndarray  # [L, N, 3] float32
    tiempos_ms: np.ndarray  # [L] float32
    glosa: str
    firmante: str
    sesion: str = ""
    origen: str = ""
    metadatos: dict = field(default_factory=dict)


# ----------------------------------------------------------------------------
# Lectura y escritura
# ----------------------------------------------------------------------------


def codificar_cuadros(cuadros: np.ndarray) -> str:
    return base64.b64encode(np.ascontiguousarray(cuadros, dtype="<f4").tobytes()).decode("ascii")


def decodificar_cuadros(texto: str, forma: Iterable[int]) -> np.ndarray:
    datos = np.frombuffer(base64.b64decode(texto), dtype="<f4")
    return datos.reshape(tuple(forma)).astype(np.float32)


def cargar_json(ruta: Path) -> Muestra:
    d = json.loads(Path(ruta).read_text(encoding="utf-8"))
    if d.get("formato") != F.VERSION:
        raise ValueError(f"{ruta}: formato {d.get('formato')!r}, se esperaba {F.VERSION!r}")
    forma = d["forma"]
    if list(forma[1:]) != [F.N_PUNTOS, F.N_COORDENADAS]:
        raise ValueError(f"{ruta}: forma {forma} incompatible")
    cuadros = decodificar_cuadros(d["cuadros_b64"], forma)
    tiempos = np.asarray(d["tiempos_ms"], dtype=np.float32)
    if len(tiempos) != len(cuadros):
        raise ValueError(f"{ruta}: {len(tiempos)} tiempos para {len(cuadros)} cuadros")
    return Muestra(
        cuadros=cuadros,
        tiempos_ms=tiempos,
        glosa=d["glosa"],
        firmante=str(d.get("firmante", "desconocido")),
        sesion=str(d.get("sesion", "")),
        origen=str(ruta),
        metadatos={k: d.get(k) for k in ("perfil", "camara", "revision", "dispositivo")},
    )


def guardar_json(muestra: Muestra, ruta: Path) -> None:
    d = {
        "formato": F.VERSION,
        "tipo": "muestra",
        "glosa": muestra.glosa,
        "firmante": muestra.firmante,
        "sesion": muestra.sesion,
        "forma": list(muestra.cuadros.shape),
        "tiempos_ms": [float(t) for t in muestra.tiempos_ms],
        "cuadros_b64": codificar_cuadros(muestra.cuadros),
        **{k: v for k, v in muestra.metadatos.items() if v is not None},
    }
    Path(ruta).write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")


def cargar_npz(ruta: Path) -> Muestra:
    z = np.load(ruta, allow_pickle=False)
    return Muestra(
        cuadros=z["cuadros"].astype(np.float32),
        tiempos_ms=z["tiempos_ms"].astype(np.float32),
        glosa=str(z["glosa"]),
        firmante=str(z["firmante"]),
        sesion=str(z["sesion"]) if "sesion" in z else "",
        origen=str(ruta),
    )


def cargar_carpeta(carpeta: Path, solo_aceptadas: bool = True) -> list[Muestra]:
    """Carga todas las muestras ``*.json`` y ``*.npz`` de una carpeta (recursivo)."""
    muestras = []
    for ruta in sorted(Path(carpeta).rglob("*")):
        if ruta.suffix == ".json" and ruta.name != "manifiesto.json":
            try:
                m = cargar_json(ruta)
            except (ValueError, KeyError, json.JSONDecodeError):
                continue
            if solo_aceptadas and m.metadatos.get("revision") == "rechazada":
                continue
            muestras.append(m)
        elif ruta.suffix == ".npz":
            muestras.append(cargar_npz(ruta))
    return muestras


# ----------------------------------------------------------------------------
# Divisiones
# ----------------------------------------------------------------------------


def dividir_por_firmante(
    muestras: list[Muestra],
    fraccion_validacion: float = 0.15,
    fraccion_prueba: float = 0.15,
    semilla: int = 7,
) -> tuple[list[Muestra], list[Muestra], list[Muestra]]:
    """Divide sin que ningún firmante aparezca en dos particiones."""
    firmantes = sorted({m.firmante for m in muestras})
    if len(firmantes) < 3:
        raise ValueError(
            f"Se necesitan al menos 3 firmantes distintos para evaluar de forma honesta; hay {len(firmantes)}."
        )
    rng = np.random.default_rng(semilla)
    rng.shuffle(firmantes)
    n = len(firmantes)
    n_prueba = max(1, int(round(n * fraccion_prueba)))
    n_val = max(1, int(round(n * fraccion_validacion)))
    prueba = set(firmantes[:n_prueba])
    val = set(firmantes[n_prueba:n_prueba + n_val])
    ent, va, pr = [], [], []
    for m in muestras:
        if m.firmante in prueba:
            pr.append(m)
        elif m.firmante in val:
            va.append(m)
        else:
            ent.append(m)
    return ent, va, pr


# ----------------------------------------------------------------------------
# Datasets
# ----------------------------------------------------------------------------


def mano_activa(cuadro: np.ndarray) -> Optional[np.ndarray]:
    """Mano que deletrea en un cuadro, canonizada como derecha (x invertida si es la izquierda).

    Se elige la mano visible con la muñeca más alta (y menor). La app usa la
    misma regla en ``EmpaquetadorLandmarks.ManoActiva``.
    """
    candidatas = []
    for inicio, es_izq in ((F.INICIO_MANO_IZQ, True), (F.INICIO_MANO_DER, False)):
        mano = cuadro[inicio:inicio + F.PUNTOS_MANO]
        if np.abs(mano[F.MANO_MUNECA]).sum() > 0:
            candidatas.append((float(mano[F.MANO_MUNECA, 1]), es_izq, mano))
    if not candidatas:
        return None
    candidatas.sort(key=lambda c: (c[0], c[1]))
    _, es_izq, mano = candidatas[0]
    mano = mano.copy()
    if es_izq:
        mano[:, 0] = -mano[:, 0]
    return mano


def cuadros_de_letra(muestra: Muestra, margen: float = 0.2) -> list[np.ndarray]:
    """Cuadros centrales de una muestra de letra (se descartan transiciones)."""
    n = len(muestra.cuadros)
    a, b = int(n * margen), max(int(n * margen) + 1, int(n * (1 - margen)))
    manos = [mano_activa(c) for c in muestra.cuadros[a:b]]
    return [m for m in manos if m is not None]


try:  # PyTorch es opcional para cargar datos
    import torch
    from torch.utils.data import Dataset

    class DatasetSenas(Dataset):
        def __init__(self, muestras: list[Muestra], clases: list[str], aumentar: bool, semilla: int = 0,
                     cfg_aumentos: Optional[ConfigAumentos] = None) -> None:
            indice = {c: i for i, c in enumerate(clases)}
            self.items = [(m.cuadros, indice[m.glosa]) for m in muestras if m.glosa in indice]
            self.aumentar = aumentar
            self.cfg = cfg_aumentos or ConfigAumentos()
            self.rng = np.random.default_rng(semilla)

        def __len__(self) -> int:
            return len(self.items)

        def __getitem__(self, i: int):
            cuadros, etiqueta = self.items[i]
            if self.aumentar:
                cuadros = aumentar_secuencia(cuadros, self.rng, self.cfg)
            x = remuestrear(cuadros)
            return torch.from_numpy(np.ascontiguousarray(x, dtype=np.float32)), etiqueta

    class DatasetDactilologia(Dataset):
        def __init__(self, muestras: list[Muestra], clases: list[str], aumentar: bool, semilla: int = 0) -> None:
            indice = {c: i for i, c in enumerate(clases)}
            self.items = []
            for m in muestras:
                if m.glosa in indice:
                    for mano in cuadros_de_letra(m):
                        self.items.append((mano, indice[m.glosa]))
            self.aumentar = aumentar
            self.rng = np.random.default_rng(semilla)

        def __len__(self) -> int:
            return len(self.items)

        def __getitem__(self, i: int):
            mano, etiqueta = self.items[i]
            if self.aumentar:
                mano = aumentar_mano(mano, self.rng)
            return torch.from_numpy(np.ascontiguousarray(mano, dtype=np.float32)), etiqueta

except ImportError:  # pragma: no cover
    pass
