"""Publica grabaciones revisadas como referencia del avatar.

Copia muestras (*.json grabadas con la app) a datos/avatar/<GLOSA>.json, solo si el
firmante consintió el uso para el avatar. Antes de publicar, una persona sorda debe
revisar que la seña esté bien hecha (la app la reproduce tal cual).

Uso:
    python publicar_avatar.py datos/recolecciones/P017/GRACIAS__abc.json [...]
"""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from app.config import Config  # noqa: E402
from app.recolecciones import AvatarAlmacen, MuestraInvalida  # noqa: E402
from app.vocabulario import Vocabulario  # noqa: E402


def main(rutas: list[str]) -> int:
    cfg = Config()
    glosas = set(Vocabulario(cfg.vocabulario).por_glosa)
    almacen = AvatarAlmacen(cfg.datos)
    errores = 0
    for r in rutas:
        try:
            destino = almacen.publicar(json.loads(Path(r).read_text(encoding="utf-8")), glosas)
            print(f"✔ {r} → {destino}")
        except (MuestraInvalida, ValueError, OSError) as e:
            print(f"✘ {r}: {e}")
            errores += 1
    return 1 if errores else 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1:]))
