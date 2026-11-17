"""Genera ``web/src/nucleo/formato.g.js`` a partir de ``lsrd_ml/formato.py`` (fuente de verdad).

    python scripts/generar_javascript.py            # escribe el archivo
    python scripts/generar_javascript.py --comprobar  # falla si está desactualizado
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import segmentacion as G  # noqa: E402

DESTINO = Path(__file__).resolve().parents[2] / "web" / "src" / "nucleo" / "formato.g.js"


def generar() -> str:
    p = G.ParametrosSegmentacion()
    c = {
        "VERSION": F.VERSION,
        "POSE_INDICES_MEDIAPIPE": list(F.POSE_INDICES_MEDIAPIPE),
        "CARA_INDICES_MEDIAPIPE": list(F.CARA_INDICES_MEDIAPIPE),
        "PUNTOS_MANO": F.PUNTOS_MANO,
        "MANO_MUNECA": F.MANO_MUNECA,
        "MANO_BASE_MEDIO": F.MANO_BASE_MEDIO,
        "INICIO_POSE": F.INICIO_POSE,
        "INICIO_MANO_IZQ": F.INICIO_MANO_IZQ,
        "INICIO_MANO_DER": F.INICIO_MANO_DER,
        "INICIO_CARA": F.INICIO_CARA,
        "N_PUNTOS": F.N_PUNTOS,
        "N_COORDENADAS": F.N_COORDENADAS,
        "VALORES_POR_CUADRO": F.N_PUNTOS * F.N_COORDENADAS,
        "IDX_NARIZ": F.IDX_NARIZ,
        "IDX_HOMBRO_IZQ": F.IDX_HOMBRO_IZQ,
        "IDX_HOMBRO_DER": F.IDX_HOMBRO_DER,
        "IDX_MUNECA_POSE_IZQ": F.IDX_MUNECA_POSE_IZQ,
        "IDX_MUNECA_POSE_DER": F.IDX_MUNECA_POSE_DER,
        "IDX_MUNECA_IZQ": F.IDX_MUNECA_IZQ,
        "IDX_MUNECA_DER": F.IDX_MUNECA_DER,
        "LONGITUD_SECUENCIA": F.LONGITUD_SECUENCIA,
        "HUESOS": [list(h) for h in F.HUESOS],
        "PERMUTACION_ESPEJO": list(F.PERMUTACION_ESPEJO),
        "SEGMENTACION": p.__dict__,
    }
    lineas = ["// Archivo generado por ml/scripts/generar_javascript.py a partir de lsrd_ml/formato.py.",
              "// No lo edite a mano: cambie formato.py y vuelva a generar.", ""]
    for k, v in c.items():
        lineas.append(f"export const {k} = {json.dumps(v, ensure_ascii=False)};")
    return "\n".join(lineas) + "\n"


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--comprobar", action="store_true")
    a = ap.parse_args()
    texto = generar()
    if a.comprobar:
        if not DESTINO.exists() or DESTINO.read_text(encoding="utf-8") != texto:
            raise SystemExit("formato.g.js está desactualizado: ejecute scripts/generar_javascript.py")
        print("formato.g.js al día")
        return
    DESTINO.parent.mkdir(parents=True, exist_ok=True)
    DESTINO.write_text(texto, encoding="utf-8")
    print(f"Escrito {DESTINO}")


if __name__ == "__main__":
    main()
