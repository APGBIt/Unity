"""Regenera las constantes del formato para la app Unity (FormatoLandmarks.g.cs)."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import csharp  # noqa: E402

if __name__ == "__main__":
    ruta = csharp.escribir()
    print(f"Escrito {ruta}")
