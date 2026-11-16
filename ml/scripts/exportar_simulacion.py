"""Exporta grabaciones sintéticas para el detector simulado de la app (Resources/LSRD/Simulacion).

Usa las mismas clases sintéticas que ``demo_sintetico.py`` (misma semilla), con firmantes
nuevos, para que el modelo de demostración las reconozca dentro de la app sin cámara.

Uso:
    python scripts/exportar_simulacion.py [--cantidad 12]
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import numpy as np  # noqa: E402

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402

SALIDA = Path(__file__).resolve().parents[2] / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Resources" / "LSRD" / "Simulacion"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--cantidad", type=int, default=12)
    ap.add_argument("--clases", type=int, default=20, help="debe coincidir con demo_sintetico.py")
    ap.add_argument("--salida", type=Path, default=SALIDA)
    a = ap.parse_args()

    rng_clases = np.random.default_rng(0)  # misma secuencia que generar_conjunto(semilla=0)
    clases = [S.nueva_clase(rng_clases, f"SENA-{k:02d}") for k in range(a.clases)]
    nombres = {c.glosa: f"DEMO-{k + 1:02d}" for k, c in enumerate(clases)}  # igual que renombrar() del demo

    rng = np.random.default_rng(123)
    a.salida.mkdir(parents=True, exist_ok=True)
    for viejo in a.salida.glob("sim_*.json"):
        viejo.unlink()
    for k in range(a.cantidad):
        f = S.nuevo_firmante(rng, f"SIM{k:02d}")
        clase = clases[int(rng.integers(0, len(clases)))]
        m = S.generar_muestra(rng, f, clase, fps=30.0)
        m.glosa = nombres[clase.glosa]
        alto = 960
        ancho = int(round(alto * f.aspecto))
        m.metadatos = {
            "camara": {"frontal": True, "espejo": False, "ancho": ancho, "alto": alto},
            "consentimiento": {"puntos_clave": True, "avatar": False, "version": "sintetico"},
            "revision": "aceptada",
            "dispositivo": "generador sintético",
        }
        D.guardar_json(m, a.salida / f"sim_{k:02d}_{m.glosa}.json")
    print(f"{a.cantidad} grabaciones sintéticas en {a.salida}")


if __name__ == "__main__":
    main()
