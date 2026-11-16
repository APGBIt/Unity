"""Entrena los modelos con grabaciones reales y los exporta para la app.

Lee las muestras grabadas con el modo recolección de la app (``*.json``) o
extraídas de videos (``*.npz``), entrena el modelo de señas y el de
dactilología, evalúa con firmantes que no participaron en el entrenamiento y
exporta ``senas.onnx`` / ``dactilologia.onnx`` con su ``.json``.

Uso:
    python scripts/entrenar.py --datos ../servidor/datos/recolecciones --salida modelos/v1
    python scripts/entrenar.py --datos ... --salida ... --prioridad 1 --unity
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import entrenamiento as E  # noqa: E402
from lsrd_ml import exportar as X  # noqa: E402
from lsrd_ml import vocabulario as V  # noqa: E402

CARPETA_UNITY = Path(__file__).resolve().parents[2] / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Resources" / "LSRD" / "Modelos"


def filtrar_clases(muestras, clases, min_muestras, min_firmantes):
    por_clase = defaultdict(list)
    for m in muestras:
        por_clase[m.glosa].append(m)
    utiles, excluidas = [], {}
    for c in clases:
        ms = por_clase.get(c, [])
        firmantes = {m.firmante for m in ms}
        if len(ms) >= min_muestras and len(firmantes) >= min_firmantes:
            utiles.append(c)
        else:
            excluidas[c] = f"{len(ms)} muestras de {len(firmantes)} firmantes"
    return utiles, excluidas


def reporte_md(nombre, ev, excluidas, clases) -> str:
    lineas = [f"# Reporte de entrenamiento — {nombre}", ""]
    lineas.append(f"Clases entrenadas: {len(clases)}. Muestras de prueba: {ev['n']} (firmantes no vistos).")
    lineas.append("")
    lineas.append(f"- Exactitud: **{ev['exactitud']:.1%}**")
    if "exactitud_top3" in ev:
        lineas.append(f"- Exactitud top-3: {ev['exactitud_top3']:.1%}")
    lineas.append("")
    lineas.append("| Firmante de prueba | Exactitud |")
    lineas.append("| --- | --- |")
    for f, acc in ev.get("por_firmante", {}).items():
        lineas.append(f"| {f} | {acc:.1%} |")
    lineas.append("")
    if ev.get("peores_clases"):
        lineas.append("Clases con menor recall: " + ", ".join(f"{c} ({r:.0%})" for c, r in ev["peores_clases"]))
        lineas.append("")
    if ev.get("confusiones_frecuentes"):
        lineas.append("| Seña real | Confundida con | Veces |")
        lineas.append("| --- | --- | --- |")
        for c in ev["confusiones_frecuentes"]:
            lineas.append(f"| {c['real']} | {c['predicha']} | {c['veces']} |")
        lineas.append("")
    if excluidas:
        lineas.append(f"Clases excluidas por falta de datos ({len(excluidas)}): "
                      + ", ".join(f"{c} ({r})" for c, r in list(excluidas.items())[:40]))
    return "\n".join(lineas) + "\n"


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--datos", type=Path, required=True)
    ap.add_argument("--salida", type=Path, required=True)
    ap.add_argument("--vocabulario", type=Path, default=V.RUTA_POR_DEFECTO)
    ap.add_argument("--prioridad", type=int, default=2)
    ap.add_argument("--epocas", type=int, default=60)
    ap.add_argument("--min-muestras", type=int, default=10)
    ap.add_argument("--min-firmantes", type=int, default=3)
    ap.add_argument("--solo-validadas", action="store_true", help="entrenar solo glosas con estado 'validada'")
    ap.add_argument("--unity", action="store_true", help="copiar los modelos al proyecto Unity")
    a = ap.parse_args()

    entradas = V.cargar(a.vocabulario)
    clases_s = V.clases_senas(entradas, a.prioridad, a.solo_validadas)
    clases_l = V.clases_dactilologia(entradas)
    muestras = D.cargar_carpeta(a.datos)
    if not muestras:
        sys.exit(f"No hay muestras en {a.datos}")
    print(f"{len(muestras)} muestras · {len({m.firmante for m in muestras})} firmantes")
    print("Muestras por firmante:", dict(Counter(m.firmante for m in muestras)))
    a.salida.mkdir(parents=True, exist_ok=True)
    resumen = {}

    senas = [m for m in muestras if m.glosa in set(clases_s)]
    clases_ok, excluidas = filtrar_clases(senas, clases_s, a.min_muestras, a.min_firmantes)
    if len(clases_ok) >= 2:
        ent, val, prueba = D.dividir_por_firmante([m for m in senas if m.glosa in set(clases_ok)])
        r = E.entrenar_senas(ent, val, clases_ok, E.ConfigEntrenamiento(epocas=a.epocas),
                             informar=lambda f: print(f"  señas · época {f['epoca']} · val {f['exactitud_val']:.1%}"))
        ev = E.evaluar(r.modelo, prueba, clases_ok)
        X.exportar_senas(r.modelo, clases_ok, a.salida, ev)
        (a.salida / "reporte_senas.md").write_text(reporte_md("señas", ev, excluidas, clases_ok), encoding="utf-8")
        resumen["senas"] = {"exactitud": ev["exactitud"], "clases": len(clases_ok), "excluidas": len(excluidas)}
        print(f"Señas: exactitud con firmantes no vistos {ev['exactitud']:.1%}")
    else:
        print("Señas: no hay datos suficientes todavía (se necesitan al menos 2 clases con datos).")

    letras = [m for m in muestras if m.glosa in set(clases_l)]
    clases_lok, excl_l = filtrar_clases(letras, clases_l, max(3, a.min_muestras // 2), a.min_firmantes)
    if len(clases_lok) >= 2:
        ent, val, prueba = D.dividir_por_firmante([m for m in letras if m.glosa in set(clases_lok)])
        r = E.entrenar_dactilologia(ent, val, clases_lok)
        ev = E.evaluar(r.modelo, prueba, clases_lok, dactilologia=True)
        X.exportar_dactilologia(r.modelo, clases_lok, a.salida, ev)
        (a.salida / "reporte_dactilologia.md").write_text(reporte_md("dactilología", ev, excl_l, clases_lok),
                                                          encoding="utf-8")
        resumen["dactilologia"] = {"exactitud": ev["exactitud"], "clases": len(clases_lok)}
        print(f"Dactilología: exactitud con firmantes no vistos {ev['exactitud']:.1%}")
    else:
        print("Dactilología: no hay datos suficientes todavía.")

    (a.salida / "resumen.json").write_text(json.dumps(resumen, ensure_ascii=False, indent=2), encoding="utf-8")
    if a.unity:
        CARPETA_UNITY.mkdir(parents=True, exist_ok=True)
        for nombre in ("senas.onnx", "senas_info.json", "dactilologia.onnx", "dactilologia_info.json"):
            if (a.salida / nombre).exists():
                shutil.copy2(a.salida / nombre, CARPETA_UNITY / nombre)
        print(f"Modelos copiados a {CARPETA_UNITY}")


if __name__ == "__main__":
    main()
