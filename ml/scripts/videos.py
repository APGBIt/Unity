"""Canal de videos: recolecta videos de LSRD con permiso y los convierte en muestras.

    python scripts/videos.py fuentes                      # qué fuentes se pueden usar y cuáles no
    python scripts/videos.py recolectar                   # descarga/indexa → datos/videos/inventario.csv
    python scripts/videos.py proponer --modelos-mediapipe modelos_mp/
                                                          # extrae puntos clave y propone anotaciones.csv
    (revisar datos/videos/anotaciones.csv: corregir glosa/firmante y poner estado = revisada)
    python scripts/videos.py muestras                     # anotaciones revisadas → datos/extraidos/*.npz
    python scripts/entrenar.py --datos datos/extraidos --salida modelos/semilla --semilla --unity

Solo se procesan las fuentes de ``fuentes/fuentes.csv`` con el permiso resuelto.
La descarga necesita ``pip install yt-dlp``.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import videos as W  # noqa: E402
from lsrd_ml import vocabulario as V  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]


def cmd_fuentes(a) -> None:
    fuentes = W.leer_fuentes(a.fuentes)
    ok, bloqueadas = W.utilizables(fuentes)
    for f in fuentes:
        estado = "LISTA" if f in ok else f"BLOQUEADA ({bloqueadas[f.id]})"
        print(f"- {f.id}: {estado} · {f.nombre} · titular: {f.titular}")
    print(f"\n{len(ok)} de {len(fuentes)} fuentes se pueden usar para entrenamiento.")


def cmd_recolectar(a) -> None:
    fuentes = W.leer_fuentes(a.fuentes)
    ok, bloqueadas = W.utilizables(fuentes)
    for fid, motivo in bloqueadas.items():
        print(f"· {fid}: omitida ({motivo})")
    filas = []
    for f in ok:
        if f.tipo == "carpeta":
            carpeta = Path(f.url) if Path(f.url).is_absolute() else RAIZ / "ml" / f.url
            if not carpeta.exists():
                print(f"· {f.id}: la carpeta {carpeta} no existe todavía")
                continue
        else:
            print(f"· {f.id}: descargando {f.url}")
            carpeta = W.descargar_fuente(f, a.videos)
        nuevas = W.indexar(f, carpeta, a.videos)
        print(f"· {f.id}: {len(nuevas)} videos")
        filas.extend(nuevas)
    W.escribir_csv(a.videos / "inventario.csv", W.CAMPOS_INVENTARIO, filas)
    print(f"Inventario: {len(filas)} videos → {a.videos / 'inventario.csv'}")


def cmd_proponer(a) -> None:
    from lsrd_ml import extraccion as X

    def extraer(ruta: Path):
        extractor = X.ExtractorMediaPipe(a.modelos_mediapipe)
        try:
            return X.extraer_video(ruta, extractor)
        finally:
            extractor.cerrar()

    inventario = W.leer_csv(a.videos / "inventario.csv")
    ruta_anot = a.videos / "anotaciones.csv"
    existentes = W.leer_csv(ruta_anot) if ruta_anot.exists() else []
    filas = W.proponer_anotaciones(inventario, a.videos, a.videos / "puntos_clave", extraer, V.cargar(a.vocabulario),
                                   existentes)
    W.escribir_csv(ruta_anot, W.CAMPOS_ANOTACION, filas)
    cuenta = Counter(f["estado"] for f in filas)
    con_glosa = sum(1 for f in filas if f["estado"] == "propuesta" and f["glosa"])
    print(f"{len(filas)} anotaciones → {ruta_anot}")
    print(f"  estados: {dict(cuenta)} · propuestas con glosa sugerida por el título: {con_glosa}")
    print("Revise el archivo: corrija glosa y firmante, y cambie estado a «revisada» o «descartada».")


def cmd_muestras(a) -> None:
    entradas = V.cargar(a.vocabulario)
    validas = {e.glosa for e in entradas}
    r = W.generar_muestras(W.leer_csv(a.videos / "anotaciones.csv"), a.videos / "puntos_clave", a.salida, validas,
                           a.incluir_propuestas)
    print(f"{r['escritas']} muestras de {len(r['por_glosa'])} señas y {len(r['firmantes'])} firmantes → {a.salida}")
    for razon, n in sorted(r["omitidas"].items(), key=lambda x: -x[1]):
        print(f"  omitidas · {razon}: {n}")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--fuentes", type=Path, default=RAIZ / "fuentes" / "fuentes.csv")
    ap.add_argument("--videos", type=Path, default=RAIZ / "ml" / "datos" / "videos")
    ap.add_argument("--vocabulario", type=Path, default=V.RUTA_POR_DEFECTO)
    sub = ap.add_subparsers(dest="orden", required=True)
    sub.add_parser("fuentes").set_defaults(f=cmd_fuentes)
    sub.add_parser("recolectar").set_defaults(f=cmd_recolectar)
    p = sub.add_parser("proponer")
    p.add_argument("--modelos-mediapipe", type=Path, required=True)
    p.set_defaults(f=cmd_proponer)
    p = sub.add_parser("muestras")
    p.add_argument("--salida", type=Path, default=RAIZ / "ml" / "datos" / "extraidos")
    p.add_argument("--incluir-propuestas", action="store_true",
                   help="usar también anotaciones sin revisar (solo para pruebas)")
    p.set_defaults(f=cmd_muestras)
    a = ap.parse_args()
    a.f(a)


if __name__ == "__main__":
    main()
