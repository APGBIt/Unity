"""Convierte videos de señas (con consentimiento) en muestras ``.npz``.

Necesita un manifiesto CSV con las columnas ``archivo,glosa,firmante,sesion``
(``archivo`` relativo a la carpeta de videos) y los modelos de MediaPipe
``pose_landmarker_full.task``, ``hand_landmarker.task`` y ``face_landmarker.task``.

Cada video debería contener una sola ejecución de la seña. Se recorta
automáticamente al segmento más largo detectado (con un margen).

Uso:
    python scripts/extraer_videos.py --videos videos/ --manifiesto videos/manifiesto.csv \\
        --modelos-mediapipe modelos_mp/ --salida datos/extraidos/
"""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import numpy as np  # noqa: E402

from lsrd_ml import extraccion as X  # noqa: E402
from lsrd_ml import segmentacion as G  # noqa: E402

MARGEN_MS = 0.0  # igual que la app: el segmento exacto, sin margen


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--videos", type=Path, required=True)
    ap.add_argument("--manifiesto", type=Path, required=True)
    ap.add_argument("--modelos-mediapipe", type=Path, required=True)
    ap.add_argument("--salida", type=Path, required=True)
    ap.add_argument("--sin-recorte", action="store_true", help="guardar el video completo")
    a = ap.parse_args()

    a.salida.mkdir(parents=True, exist_ok=True)
    with open(a.manifiesto, encoding="utf-8", newline="") as fh:
        filas = list(csv.DictReader(fh))
    ok = fallos = 0
    for k, fila in enumerate(filas):
        ruta = a.videos / fila["archivo"]
        extractor = X.ExtractorMediaPipe(a.modelos_mediapipe)  # uno por video: los tiempos reinician
        try:
            cuadros, tiempos = X.extraer_video(ruta, extractor)
        except Exception as e:  # noqa: BLE001
            print(f"[{k + 1}/{len(filas)}] {ruta.name}: ERROR {e}")
            fallos += 1
            continue
        finally:
            extractor.cerrar()
        if not a.sin_recorte:
            segmentos = G.segmentar_secuencia(cuadros, tiempos)
            if segmentos:
                ini, fin = max(segmentos, key=lambda s: s[1] - s[0])
                sel = (tiempos >= ini - MARGEN_MS) & (tiempos <= fin + MARGEN_MS)
                cuadros, tiempos = cuadros[sel], tiempos[sel]
            else:
                print(f"[{k + 1}/{len(filas)}] {ruta.name}: no se detectó ninguna seña; se guarda completo")
        destino = a.salida / f"{fila['firmante']}__{fila['glosa']}__{ruta.stem}.npz"
        np.savez_compressed(destino, cuadros=cuadros.astype(np.float32), tiempos_ms=tiempos.astype(np.float32),
                            glosa=fila["glosa"], firmante=fila["firmante"], sesion=fila.get("sesion", ""))
        ok += 1
        print(f"[{k + 1}/{len(filas)}] {ruta.name}: {len(cuadros)} cuadros → {destino.name}")
    print(f"Listo: {ok} videos procesados, {fallos} con error")


if __name__ == "__main__":
    main()
