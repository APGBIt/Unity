"""Convierte paquetes de señas de la app web en muestras para entrenar.

    python scripts/importar_paquete_web.py paquetes/*.json --salida datos/extraidos

Solo entran los paquetes cuya persona marcó la autorización para entrenamiento
al guardarlos (``consentimiento_entrenamiento``). Cada paquete cuenta como un
firmante: use ``--firmante`` si conoce el identificador, o se toma el nombre
del archivo.

Las señas de un paquete son las que cada persona enseñó en su teléfono. Antes de
entrenar con ellas, una persona sorda o un intérprete debe revisar que cada
grabación corresponde a la seña de LSRD que dice ser.
"""

from __future__ import annotations

import argparse
import base64
import json
import re
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import vocabulario as V  # noqa: E402

TIPO = "senas-rd/paquete"


def convertir(paquete: dict, firmante: str, salida: Path, glosas_validas: set[str]) -> dict:
    """Escribe un ``.npz`` por ejemplo. Devuelve un resumen."""
    if paquete.get("tipo") != TIPO:
        raise ValueError("No es un paquete de señas de Señas RD")
    if paquete.get("formato") != F.VERSION:
        raise ValueError(f"Formato de puntos clave distinto: {paquete.get('formato')}")
    if not paquete.get("consentimiento_entrenamiento"):
        raise PermissionError("El paquete no trae autorización para entrenamiento")
    salida.mkdir(parents=True, exist_ok=True)
    resumen = {"escritas": 0, "fuera_de_vocabulario": 0, "invalidas": 0}
    esperado = F.LONGITUD_SECUENCIA * F.N_PUNTOS * F.N_COORDENADAS
    for k, e in enumerate(paquete.get("ejemplos", [])):
        glosa = str(e.get("glosa", ""))
        if glosa not in glosas_validas:
            resumen["fuera_de_vocabulario"] += 1
            continue
        try:
            datos = np.frombuffer(base64.b64decode(e["sec_b64"]), dtype="<f4")
        except Exception:
            resumen["invalidas"] += 1
            continue
        if datos.size != esperado or not np.isfinite(datos).all():
            resumen["invalidas"] += 1
            continue
        cuadros = datos.reshape(F.LONGITUD_SECUENCIA, F.N_PUNTOS, F.N_COORDENADAS).astype(np.float32)
        duracion = float(e.get("duracion_ms") or 1000.0)
        tiempos = np.linspace(0.0, max(200.0, duracion), F.LONGITUD_SECUENCIA, dtype=np.float32)
        nombre = f"{firmante}__{glosa}__web_{k:04d}.npz"
        np.savez_compressed(salida / nombre, cuadros=cuadros, tiempos_ms=tiempos, glosa=glosa, firmante=firmante, sesion="web")
        resumen["escritas"] += 1
    return resumen


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("paquetes", nargs="+", type=Path)
    ap.add_argument("--salida", type=Path, required=True)
    ap.add_argument("--firmante", default=None, help="identificador del firmante (por defecto, el nombre del archivo)")
    ap.add_argument("--vocabulario", type=Path, default=V.RUTA_POR_DEFECTO)
    a = ap.parse_args()
    validas = {e.glosa for e in V.cargar(a.vocabulario)}
    for ruta in a.paquetes:
        firmante = a.firmante or re.sub(r"[^A-Za-z0-9]+", "-", ruta.stem).strip("-") or "web"
        try:
            r = convertir(json.loads(ruta.read_text(encoding="utf-8")), firmante, a.salida, validas)
            print(f"{ruta.name}: {r}")
        except (ValueError, PermissionError) as e:
            print(f"{ruta.name}: omitido ({e})")


if __name__ == "__main__":
    main()
