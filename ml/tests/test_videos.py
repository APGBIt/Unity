import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402
from lsrd_ml import videos as W  # noqa: E402
from lsrd_ml import vocabulario as V  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]


def _fuente(**k):
    base = dict(id="f1", nombre="F", url="https://ejemplo.invalid/v", tipo="video", titular="T",
                permiso="autorizado", evidencia_permiso="Convenio 01", uso=("entrenamiento",))
    base.update(k)
    return W.Fuente(**base)


def test_catalogo_del_repositorio_bloquea_lo_pendiente():
    fuentes = W.leer_fuentes(RAIZ / "fuentes" / "fuentes.csv")
    ok, bloqueadas = W.utilizables(fuentes)
    assert "diccionario-oficial" in bloqueadas and "pendiente" in bloqueadas["diccionario-oficial"]
    assert [f.id for f in ok] == ["grabaciones-propias"]


@pytest.mark.parametrize("cambio,motivo", [
    (dict(permiso="pendiente"), "pendiente"),
    (dict(evidencia_permiso=""), "evidencia"),
    (dict(uso=("referencia",)), "no cubre"),
    (dict(permiso="si"), "desconocido"),
    (dict(url=""), "falta la dirección"),
])
def test_motivos_de_bloqueo(cambio, motivo):
    assert motivo in _fuente(**cambio).motivo_bloqueo()


def test_no_descarga_fuentes_sin_permiso(tmp_path):
    llamadas = []
    with pytest.raises(PermissionError):
        W.descargar_fuente(_fuente(permiso="pendiente"), tmp_path, ejecutar=lambda *a, **k: llamadas.append(a))
    assert not llamadas
    W.descargar_fuente(_fuente(), tmp_path, ejecutar=lambda *a, **k: llamadas.append(a))
    assert len(llamadas) == 1 and "yt_dlp" in llamadas[0][0]


def test_glosa_de_titulo():
    indice = W.indice_de_terminos(V.cargar())
    assert W.glosa_de_titulo("Buenos Días", indice) == "BUENOS-DIAS"
    assert W.glosa_de_titulo("Seña de: CÉDULA (LSRD)", indice) == "CEDULA"
    assert W.glosa_de_titulo("Año", indice) == "AÑO"
    assert W.glosa_de_titulo("Clase 3 de lengua de señas", indice) == ""


def _video_sintetico(rng, firmante, clases):
    cuadros, tiempos, t0 = [], [], 0.0
    for c in clases:
        m = S.generar_muestra(rng, firmante, c, fps=30, con_reposo=True)
        cuadros.append(m.cuadros)
        tiempos.append(m.tiempos_ms - m.tiempos_ms[0] + t0)
        t0 = float(tiempos[-1][-1]) + 33.0
    return np.concatenate(cuadros), np.concatenate(tiempos)


def test_de_videos_a_muestras(tmp_path):
    rng = np.random.default_rng(3)
    firmante = S.nuevo_firmante(rng, "P001")
    videos = tmp_path / "videos"
    (videos / "propias" / "P001").mkdir(parents=True)
    contenido = {}
    for nombre, n in (("gracias", 2), ("clase", 3)):
        ruta = videos / "propias" / "P001" / f"{nombre}.mp4"
        ruta.write_bytes(nombre.encode())
        contenido[ruta] = _video_sintetico(rng, firmante, [S.nueva_clase(rng, f"{nombre}{k}") for k in range(n)])

    fuente = _fuente(id="propias", tipo="carpeta", permiso="propio", url=str(videos / "propias"))
    inventario = W.indexar(fuente, videos / "propias", videos)
    assert [v["firmante"] for v in inventario] == ["P001", "P001"]
    assert all(len(v["sha256"]) == 64 for v in inventario)

    extraidos = []

    def extraer(ruta):
        extraidos.append(ruta)
        return contenido[ruta]

    entradas = V.cargar()
    cache = videos / "puntos_clave"
    anot = W.proponer_anotaciones(inventario, videos, cache, extraer, entradas)
    por_video = {}
    for a in anot:
        por_video.setdefault(Path(a["archivo"]).stem, []).append(a)
    assert len(por_video["gracias"]) == 2 and len(por_video["clase"]) == 3
    assert all(a["glosa"] == "GRACIAS" and a["sugerencia"] == "titulo" for a in por_video["gracias"])
    assert all(a["glosa"] == "" for a in por_video["clase"])

    # La revisión humana: confirma un video y etiqueta parte del otro.
    for a in por_video["gracias"]:
        a["estado"] = "revisada"
    por_video["clase"][0].update(glosa="HOLA", estado="revisada")
    por_video["clase"][1].update(glosa="NO-EXISTE", estado="revisada")

    # Volver a proponer no repite la extracción ni pisa lo revisado.
    de_nuevo = W.proponer_anotaciones(inventario, videos, cache, extraer, entradas, existentes=anot)
    assert de_nuevo == anot and len(extraidos) == 2

    salida = tmp_path / "extraidos"
    r = W.generar_muestras(anot, cache, salida, {e.glosa for e in entradas})
    assert r["escritas"] == 3 and r["por_glosa"] == {"GRACIAS": 2, "HOLA": 1}
    assert r["omitidas"] == {"glosa fuera del vocabulario": 1, "estado propuesta": 1}
    muestras = D.cargar_carpeta(salida)
    assert sorted(m.glosa for m in muestras) == ["GRACIAS", "GRACIAS", "HOLA"]
    assert all(m.firmante == "P001" and len(m.cuadros) >= 4 for m in muestras)


def test_dividir_por_toma():
    def m(glosa, k):
        return D.Muestra(np.zeros((4, 78, 3), np.float32), np.arange(4, dtype=np.float32), glosa, "P1", str(k))

    ent, val = D.dividir_por_toma([m("A", k) for k in range(8)] + [m("B", 0), m("B", 1)] + [m("C", 0)])
    assert sorted(x.glosa for x in val) == ["A", "A", "B"]
    assert sum(1 for x in ent if x.glosa == "C") == 1 and len(ent) + len(val) == 11


def test_entrenamiento_semilla_de_punta_a_punta(tmp_path):
    """Dos personas, pocas tomas: entrena, exporta y deja el modelo marcado como semilla."""
    rng = np.random.default_rng(5)
    glosas = ["HOLA", "GRACIAS", "CEDULA"]
    clases = [S.nueva_clase(rng, g) for g in glosas]
    datos = tmp_path / "datos"
    datos.mkdir()
    for p in ("P001", "P002"):
        f = S.nuevo_firmante(rng, p)
        for c, g in zip(clases, glosas):
            for k in range(4):
                mu = S.generar_muestra(rng, f, c, fps=30)
                np.savez_compressed(datos / f"{p}__{g}__{k}.npz", cuadros=mu.cuadros, tiempos_ms=mu.tiempos_ms,
                                    glosa=g, firmante=p, sesion=str(k))
    salida = tmp_path / "modelo"
    r = subprocess.run([sys.executable, str(RAIZ / "ml" / "scripts" / "entrenar.py"), "--datos", str(datos),
                        "--salida", str(salida), "--semilla", "--epocas", "6"], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr[-2000:]
    import json
    info = json.loads((salida / "senas_info.json").read_text(encoding="utf-8"))
    assert info["clases"] == glosas and "SEMILLA" in info["nota"]
    assert info["metricas"]["tipo_medicion"].startswith("tomas apartadas")
    assert "Modelo semilla" in (salida / "reporte_senas.md").read_text(encoding="utf-8")

    # Sin --semilla los mismos datos no alcanzan: no se exporta nada.
    estricta = tmp_path / "estricto"
    r = subprocess.run([sys.executable, str(RAIZ / "ml" / "scripts" / "entrenar.py"), "--datos", str(datos),
                        "--salida", str(estricta)], capture_output=True, text=True)
    assert r.returncode == 0 and not (estricta / "senas.onnx").exists()
