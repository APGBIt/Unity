"""Prueba de punta a punta del canal de entrenamiento con datos SINTÉTICOS.

Genera firmantes y señas sintéticas, entrena los dos modelos, evalúa con
firmantes no vistos, exporta a ONNX y verifica la paridad con ONNX Runtime.
Los modelos resultantes sirven para probar la app (la cámara, la segmentación,
la inferencia y la interfaz), pero NO reconocen LSRD: sus clases se llaman
DEMO-01, DEMO-02… y responden a movimientos sintéticos.

Uso:
    python scripts/demo_sintetico.py --salida modelos/demo [--unity]
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import torch  # noqa: E402

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import entrenamiento as E  # noqa: E402
from lsrd_ml import exportar as X  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402

CARPETA_UNITY = Path(__file__).resolve().parents[2] / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Resources" / "LSRD" / "Modelos"
NOTA = "MODELO DE DEMOSTRACIÓN entrenado con datos sintéticos. No reconoce LSRD."


def renombrar(muestras, clases, prefijo):
    mapa = {c: (c if c == "_FONDO" else f"{prefijo}-{k:02d}") for k, c in enumerate(clases)}
    for m in muestras:
        m.glosa = mapa[m.glosa]
    return [mapa[c] for c in clases]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--salida", type=Path, default=Path("modelos/demo"))
    ap.add_argument("--clases", type=int, default=20)
    ap.add_argument("--firmantes", type=int, default=12)
    ap.add_argument("--repeticiones", type=int, default=6)
    ap.add_argument("--epocas", type=int, default=15)
    ap.add_argument("--unity", action="store_true", help="copiar los modelos a la carpeta Modelos del proyecto Unity")
    ap.add_argument("--hilos", type=int, default=0)
    a = ap.parse_args()
    if a.hilos:
        torch.set_num_threads(a.hilos)

    t0 = time.time()
    print("1/4 Generando datos sintéticos…")
    muestras, clases = S.generar_conjunto(a.clases, a.firmantes, a.repeticiones, semilla=0)
    clases = renombrar(muestras, clases, "DEMO")
    ent, val, prueba = D.dividir_por_firmante(muestras)
    print(f"    {len(muestras)} muestras · {len(clases)} clases · firmantes de prueba: "
          f"{sorted({m.firmante for m in prueba})}")

    print("2/4 Entrenando modelo de señas…")
    r = E.entrenar_senas(ent, val, clases, E.ConfigEntrenamiento(epocas=a.epocas, paciencia=a.epocas),
                         informar=lambda f: print(f"    época {f['epoca']:>2} · pérdida {f['perdida']:.3f} · "
                                                  f"exactitud val {f['exactitud_val']:.1%}"))
    ev = E.evaluar(r.modelo, prueba, clases)
    print(f"    Firmantes no vistos: exactitud {ev['exactitud']:.1%} · top-3 {ev['exactitud_top3']:.1%}")
    info_s = X.exportar_senas(r.modelo, clases, a.salida, ev, nombre="senas", nota=NOTA)
    print(f"    ONNX verificado (diferencia máx. {info_s['diferencia_onnx']:.1e})")

    print("3/4 Entrenando modelo de dactilología…")
    letras, clases_l = S.generar_letras(n_letras=10, n_firmantes=8, repeticiones=4)
    clases_l = renombrar(letras, clases_l, "DEMO-LETRA")
    le, lv, lp = D.dividir_por_firmante(letras)
    rl = E.entrenar_dactilologia(le, lv, clases_l, E.ConfigEntrenamiento(epocas=25, lote=256, tasa=2e-3, paciencia=25))
    evl = E.evaluar(rl.modelo, lp, clases_l, dactilologia=True)
    print(f"    Firmantes no vistos: exactitud {evl['exactitud']:.1%}")
    info_l = X.exportar_dactilologia(rl.modelo, clases_l, a.salida, evl, nombre="dactilologia", nota=NOTA)

    print("4/4 Guardando…")
    reporte = {
        "nota": NOTA,
        "senas": {k: ev[k] for k in ("exactitud", "exactitud_top3", "por_firmante")},
        "dactilologia": {k: evl[k] for k in ("exactitud", "por_firmante")},
        "segundos": round(time.time() - t0, 1),
    }
    (a.salida / "reporte_demo.json").write_text(json.dumps(reporte, ensure_ascii=False, indent=2), encoding="utf-8")
    if a.unity:
        CARPETA_UNITY.mkdir(parents=True, exist_ok=True)
        for nombre in ("senas.onnx", "senas_info.json", "dactilologia.onnx", "dactilologia_info.json"):
            shutil.copy2(a.salida / nombre, CARPETA_UNITY / nombre)
        print(f"    Copiados a {CARPETA_UNITY}")
    print(f"Listo en {reporte['segundos']} s → {a.salida}")
    _ = info_l


if __name__ == "__main__":
    main()
