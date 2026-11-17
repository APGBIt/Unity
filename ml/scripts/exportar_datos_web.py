"""Datos sintéticos para probar la app web (no son LSRD).

Escribe dos archivos:

* ``web/tests/datos/sintetico.json.gz`` — tomas ya remuestreadas a 32 cuadros, de
  varias personas, para medir el reconocedor por ejemplos.
* ``web/e2e/datos/flujo.json.gz`` — tomas completas a 30 cuadros por segundo con
  reposo antes y después, para simular la cámara en la prueba de punta a punta.

Con ``--grande RUTA`` escribe además un conjunto mayor (para ``npm run evaluar``)
que no se guarda en el repositorio.

Los puntos se guardan como enteros de 16 bits (1/8192 de alto de imagen) para
que los archivos pesen poco.
"""

from __future__ import annotations

import argparse
import base64
import gzip
import json
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import remuestreo as R  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
ESCALA = 8192


def q16(x: np.ndarray) -> str:
    return base64.b64encode(np.clip(np.round(x * ESCALA), -32768, 32767).astype("<i2").tobytes()).decode("ascii")


def escribir(ruta: Path, datos: dict) -> None:
    ruta.parent.mkdir(parents=True, exist_ok=True)
    with gzip.GzipFile(ruta, "wb", mtime=0) as fh:
        fh.write(json.dumps(datos, separators=(",", ":")).encode("utf-8"))
    print(f"{ruta}: {ruta.stat().st_size / 1024:.0f} KB")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--grande", type=Path, default=None, help="ruta del conjunto grande de evaluación (.json.gz)")
    a = ap.parse_args()
    rng = np.random.default_rng(2026)
    ensenadas = [S.nueva_clase(rng, f"S{k:02d}") for k in range(8)]
    letras = [S.nueva_letra(rng, f"L{k:02d}") for k in range(4)]
    desconocidas = [S.nueva_clase(rng, f"X{k:02d}") for k in range(4)]
    firmantes = [S.nuevo_firmante(rng, f"P{k:02d}") for k in range(4)]
    firmantes[1].zurdo = False
    firmantes[2].zurdo = True  # una persona zurda, para probar el espejo
    firmantes[0].zurdo = False
    firmantes[3].zurdo = False

    def conjunto(clases, extranas, tomas_por_clase, tomas_extranas):
        muestras = []
        for f in firmantes:
            for c in clases:
                for _ in range(tomas_por_clase):
                    m = S.generar_muestra(rng, f, c, fps=30)
                    muestras.append({"glosa": c.glosa, "firmante": f.id, "zurdo": f.zurdo, "sec": q16(R.remuestrear(m.cuadros))})
            for c in extranas:
                for _ in range(tomas_extranas):
                    m = S.generar_muestra(rng, f, c, fps=30)
                    muestras.append({"glosa": c.glosa, "firmante": f.id, "zurdo": f.zurdo, "desconocida": True,
                                     "sec": q16(R.remuestrear(m.cuadros))})
        return {"escala": ESCALA, "nota": "Datos sintéticos. No son LSRD.", "muestras": muestras}

    if a.grande:
        escribir(a.grande, conjunto(ensenadas + letras, desconocidas, 6, 3))
    firmantes_todos, firmantes = firmantes, firmantes[:3]
    escribir(RAIZ / "web" / "tests" / "datos" / "sintetico.json.gz", conjunto(ensenadas[:4] + letras[:2], desconocidas[:2], 5, 2))
    firmantes = firmantes_todos

    f = firmantes[0]
    tomas = []
    for c in ensenadas[:3]:
        for k in range(6):
            m = S.generar_muestra(rng, f, c, fps=30, con_reposo=True)
            tomas.append({"glosa": c.glosa, "n": len(m.cuadros), "tiempos_ms": [round(float(t), 2) for t in m.tiempos_ms],
                          "cuadros": q16(m.cuadros)})
    m = S.generar_muestra(rng, f, desconocidas[0], fps=30, con_reposo=True)
    tomas.append({"glosa": "DESCONOCIDA", "n": len(m.cuadros), "tiempos_ms": [round(float(t), 2) for t in m.tiempos_ms],
                  "cuadros": q16(m.cuadros)})
    escribir(RAIZ / "web" / "e2e" / "datos" / "flujo.json.gz",
             {"escala": ESCALA, "aspecto": f.aspecto, "nota": "Datos sintéticos. No son LSRD.", "tomas": tomas})


if __name__ == "__main__":
    main()
