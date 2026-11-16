"""Genera los "vectores de oro" para las pruebas de paridad Python ↔ C#.

Las pruebas de la app (Unity EditMode y la verificación con .NET) cargan este
archivo y comprueban que el remuestreo, la segmentación, la selección de la mano
activa, el empaquetado de MediaPipe y la inferencia de los modelos dan lo mismo
que aquí.

Uso:
    python scripts/generar_vectores_oro.py [--modelos carpeta_con_onnx]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import numpy as np  # noqa: E402

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import extraccion as X  # noqa: E402
from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import remuestreo as R  # noqa: E402
from lsrd_ml import segmentacion as G  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
SALIDA = RAIZ / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Tests" / "Datos" / "vectores_oro.json"
MODELOS = RAIZ / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Resources" / "LSRD" / "Modelos"


def b64(x: np.ndarray) -> str:
    return D.codificar_cuadros(np.asarray(x, dtype=np.float32))


def caso_segmentacion(nombre: str, cuadros: np.ndarray, tiempos: np.ndarray, p: G.ParametrosSegmentacion) -> dict:
    mediciones = []
    anterior, t_ant = None, None
    for c, t in zip(cuadros, tiempos):
        dt = (float(t) - float(t_ant)) / 1000.0 if t_ant is not None else 0.0
        activo, vel = G.medir_cuadro(c, anterior, dt, p)
        mediciones.append([1 if activo else 0, -1.0 if vel == G.INFINITO else float(vel)])
        anterior, t_ant = c, t
    segmentos = G.segmentar_secuencia(cuadros, tiempos, p)
    return {
        "nombre": nombre,
        "forma": list(cuadros.shape),
        "cuadros_b64": b64(cuadros),
        "tiempos_ms": [float(t) for t in tiempos],
        "mediciones": mediciones,
        "segmentos": [[float(a), float(b)] for a, b in segmentos],
    }


def flujo(rng, partes) -> tuple[np.ndarray, np.ndarray]:
    """Concatena muestras en un solo flujo con tiempos continuos."""
    cuadros, tiempos, t0 = [], [], 0.0
    for m in partes:
        cuadros.append(m.cuadros)
        tiempos.append(m.tiempos_ms - m.tiempos_ms[0] + t0)
        t0 = float(tiempos[-1][-1]) + 33.0
    return np.concatenate(cuadros), np.concatenate(tiempos).astype(np.float32)


def casos_segmentacion(p: G.ParametrosSegmentacion) -> list[dict]:
    rng = np.random.default_rng(42)
    f = S.nuevo_firmante(rng, "ORO")
    f.zurdo = False
    clases = [S.nueva_clase(rng, f"C{k}") for k in range(4)]
    casos = []

    # 1) tres señas separadas por reposo
    partes = [S.generar_muestra(rng, f, c, fps=30, con_reposo=True) for c in clases[:3]]
    casos.append(caso_segmentacion("tres_senas_con_reposo", *flujo(rng, partes), p))

    # 2) seña estática sostenida (debe cortarse por quietud)
    estatica = S.ClaseSintetica(
        glosa="NUM", puntos=np.array([[0.6, -0.4], [0.6, -0.4]]), forma_ini=np.array([0, 0, 1, 1, 1, 0.2]),
        forma_fin=None, angulo_ini=-np.pi / 2, angulo_fin=-np.pi / 2, dos_manos=0, cejas=0, boca=0,
        repeticiones=1, duracion_s=1.4,
    )
    casos.append(caso_segmentacion("sena_estatica", *flujo(rng, [S.generar_muestra(rng, f, estatica, fps=30, con_reposo=True)]), p))

    # 3) firmado continuo largo sin bajar las manos (corte forzado por max_ms)
    largas = [S.generar_muestra(rng, f, c, fps=20) for c in clases for _ in range(2)]
    cuadros, tiempos = flujo(rng, largas)
    casos.append(caso_segmentacion("firmado_continuo", cuadros, tiempos, p))

    # 4) sin hombros visibles (solo manos y cara)
    cuadros, tiempos = flujo(rng, [S.generar_muestra(rng, f, clases[3], fps=15, con_reposo=True)])
    cuadros = cuadros.copy()
    cuadros[:, F.INICIO_POSE:F.INICIO_MANO_IZQ] = 0.0
    casos.append(caso_segmentacion("sin_hombros", cuadros, tiempos, p))

    # 5) aparición muy breve de una mano (debe descartarse)
    breve = S.generar_muestra(rng, f, S.clase_fondo(), fps=30, con_reposo=True)
    casos.append(caso_segmentacion("aparicion_breve", breve.cuadros[:12], breve.tiempos_ms[:12], p))
    return casos


def casos_mano_activa() -> list[dict]:
    rng = np.random.default_rng(5)
    f = S.nuevo_firmante(rng, "ORO")
    m = S.generar_muestra(rng, f, S.nueva_clase(rng, "X"), fps=30)
    casos = []
    for k in (0, 5, 10):
        casos.append(m.cuadros[k].copy())
    solo_izq = m.cuadros[3].copy()
    solo_izq[F.INICIO_MANO_DER:F.INICIO_CARA] = 0
    solo_izq[F.INICIO_MANO_IZQ:F.INICIO_MANO_DER] = m.cuadros[3][F.INICIO_MANO_DER:F.INICIO_CARA]
    casos.append(solo_izq)
    vacio = m.cuadros[4].copy()
    vacio[F.INICIO_MANO_IZQ:F.INICIO_CARA] = 0
    casos.append(vacio)
    salida = []
    for c in casos:
        esperado = D.mano_activa(c)
        salida.append({"cuadro_b64": b64(c), "esperado_b64": None if esperado is None else b64(esperado)})
    return salida


def casos_empaquetado() -> list[dict]:
    rng = np.random.default_rng(9)
    casos = []
    for k in range(3):
        pose = rng.uniform(0, 1, size=(33, 4)).astype(np.float32)
        pose[:, 3] = rng.uniform(0, 1, size=33)  # visibilidad
        mi = rng.uniform(0, 1, size=(21, 3)).astype(np.float32) if k != 1 else None
        md = rng.uniform(0, 1, size=(21, 3)).astype(np.float32)
        cara = rng.uniform(0, 1, size=(478, 3)).astype(np.float32) if k != 2 else None
        aspecto = [0.75, 4 / 3, 16 / 9][k]

        def pts(a, con_vis=False):
            if a is None:
                return None
            return [SimpleNamespace(x=float(r[0]), y=float(r[1]), z=float(r[2]),
                                    visibility=float(r[3]) if con_vis else None) for r in a]

        esperado = X.empaquetar(pts(pose, True), pts(mi), pts(md), pts(cara), aspecto)
        casos.append({
            "aspecto": aspecto,
            "pose_b64": b64(pose),
            "mano_izq_b64": None if mi is None else b64(mi),
            "mano_der_b64": b64(md),
            "cara_b64": None if cara is None else b64(cara),
            "esperado_b64": b64(esperado),
        })
    return casos


def casos_modelo(carpeta: Path) -> dict:
    import onnxruntime as ort

    salida = {}
    rng = np.random.default_rng(11)
    ruta = carpeta / "senas.onnx"
    if ruta.exists():
        ses = ort.InferenceSession(str(ruta), providers=["CPUExecutionProvider"])
        f = S.nuevo_firmante(rng, "ORO")
        entradas = []
        for c in [S.nueva_clase(rng, "X") for _ in range(2)] + [S.clase_fondo()]:
            m = S.generar_muestra(rng, f, c, fps=30)
            entradas.append(R.remuestrear(m.cuadros))
        entradas.append(np.zeros((F.LONGITUD_SECUENCIA, F.N_PUNTOS, 3), dtype=np.float32))
        salida["senas"] = {
            "archivo": "senas.onnx",
            "casos": [{"entrada_b64": b64(x), "probs": ses.run(["probs"], {"landmarks": x[None]})[0][0].tolist()}
                      for x in entradas],
        }
    ruta = carpeta / "dactilologia.onnx"
    if ruta.exists():
        ses = ort.InferenceSession(str(ruta), providers=["CPUExecutionProvider"])
        letras, _ = S.generar_letras(n_letras=3, n_firmantes=1, repeticiones=1, semilla=3)
        entradas = [D.mano_activa(m.cuadros[len(m.cuadros) // 2]) for m in letras]
        salida["dactilologia"] = {
            "archivo": "dactilologia.onnx",
            "casos": [{"entrada_b64": b64(x), "probs": ses.run(["probs"], {"mano": x[None].astype(np.float32)})[0][0].tolist()}
                      for x in entradas if x is not None],
        }
    return salida


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--modelos", type=Path, default=MODELOS)
    ap.add_argument("--salida", type=Path, default=SALIDA)
    a = ap.parse_args()
    p = G.ParametrosSegmentacion()
    datos = {
        "formato": F.VERSION,
        "remuestreo": [{"longitud": n, "t": F.LONGITUD_SECUENCIA, "indices": R.indices(n)}
                       for n in (1, 5, 31, 32, 33, 47, 64, 90, 121)],
        "segmentacion": {"parametros": p.__dict__, "casos": casos_segmentacion(p)},
        "mano_activa": casos_mano_activa(),
        "empaquetado": casos_empaquetado(),
        "modelos": casos_modelo(a.modelos),
    }
    a.salida.parent.mkdir(parents=True, exist_ok=True)
    a.salida.write_text(json.dumps(datos, ensure_ascii=False), encoding="utf-8")
    segs = {c["nombre"]: len(c["segmentos"]) for c in datos["segmentacion"]["casos"]}
    print(f"Escrito {a.salida} · segmentos por caso: {segs} · modelos: {list(datos['modelos'])}")


if __name__ == "__main__":
    main()
