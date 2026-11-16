import sys
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import aumentos as A  # noqa: E402
from lsrd_ml import csharp  # noqa: E402
from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import extraccion as X  # noqa: E402
from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import remuestreo as R  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402
from lsrd_ml import vocabulario as V  # noqa: E402


def test_disposicion_del_formato():
    assert F.N_PUNTOS == 78
    assert (F.INICIO_MANO_IZQ, F.INICIO_MANO_DER, F.INICIO_CARA) == (7, 28, 49)
    assert len(set(F.CARA_INDICES_MEDIAPIPE)) == len(F.CARA_INDICES_MEDIAPIPE)
    assert all(i < 468 for i in F.CARA_INDICES_MEDIAPIPE)


def test_espejo_es_involucion_y_cruza_lados():
    perm = np.array(F.PERMUTACION_ESPEJO)
    assert sorted(perm) == list(range(F.N_PUNTOS))
    assert (perm[perm] == np.arange(F.N_PUNTOS)).all()
    assert perm[F.IDX_HOMBRO_IZQ] == F.IDX_HOMBRO_DER
    assert perm[F.IDX_MUNECA_IZQ] == F.IDX_MUNECA_DER


def test_espejo_doble_devuelve_original():
    m = S.generar_muestra(np.random.default_rng(0), S.nuevo_firmante(np.random.default_rng(1), "X"),
                          S.nueva_clase(np.random.default_rng(2), "A"))
    np.testing.assert_allclose(A.espejo(A.espejo(m.cuadros)), m.cuadros)


def test_csharp_generado_esta_al_dia():
    assert csharp.RUTA_UNITY.exists(), "Ejecute scripts/generar_csharp.py"
    assert csharp.RUTA_UNITY.read_text(encoding="utf-8") == csharp.generar(), \
        "FormatoLandmarks.g.cs está desactualizado: ejecute scripts/generar_csharp.py"


@pytest.mark.parametrize("largo", [1, 5, 31, 32, 33, 90])
def test_remuestreo(largo):
    idx = R.indices(largo)
    assert len(idx) == F.LONGITUD_SECUENCIA
    assert idx == sorted(idx)
    assert 0 <= idx[0] and idx[-1] <= largo - 1
    if largo == 32:
        assert idx == list(range(32))


def test_json_ida_y_vuelta(tmp_path):
    m = S.generar_muestra(np.random.default_rng(0), S.nuevo_firmante(np.random.default_rng(1), "P1"),
                          S.nueva_clase(np.random.default_rng(2), "HOLA"))
    D.guardar_json(m, tmp_path / "m.json")
    r = D.cargar_json(tmp_path / "m.json")
    np.testing.assert_array_equal(r.cuadros, m.cuadros)
    assert r.glosa == "HOLA" and r.firmante == "P1"


def test_division_por_firmante_sin_solapamiento():
    muestras, _ = S.generar_conjunto(n_clases=3, n_firmantes=7, repeticiones=1)
    ent, val, pr = D.dividir_por_firmante(muestras)
    fe, fv, fp = ({m.firmante for m in x} for x in (ent, val, pr))
    assert fe and fv and fp
    assert not (fe & fv) and not (fe & fp) and not (fv & fp)


def test_division_exige_tres_firmantes():
    muestras, _ = S.generar_conjunto(n_clases=2, n_firmantes=2, repeticiones=1)
    with pytest.raises(ValueError):
        D.dividir_por_firmante(muestras)


def test_mano_activa_prefiere_la_mas_alta_y_canoniza_izquierda():
    c = np.zeros((F.N_PUNTOS, 3), np.float32)
    c[F.INICIO_MANO_IZQ:F.INICIO_MANO_DER] = [0.6, 0.4, 0.0]
    c[F.INICIO_MANO_DER:F.INICIO_CARA] = [0.3, 0.8, 0.0]
    m = D.mano_activa(c)
    assert m is not None and m[0, 0] == pytest.approx(-0.6)
    assert D.mano_activa(np.zeros_like(c)) is None


def test_aumentos_conservan_ausentes_en_cero():
    m = S.generar_muestra(np.random.default_rng(0), S.nuevo_firmante(np.random.default_rng(1), "X"),
                          S.nueva_clase(np.random.default_rng(2), "A"))
    m.cuadros[:, F.INICIO_CARA:] = 0
    x = A.aumentar_secuencia(m.cuadros, np.random.default_rng(3), A.ConfigAumentos(prob_espejo=0.0))
    assert np.all(x[:, F.INICIO_CARA:] == 0)


def test_empaquetar_mediapipe():
    def pts(n, v=0.5, vis=None):
        return [SimpleNamespace(x=v, y=v / 2, z=0.1, visibility=vis) for _ in range(n)]

    out = X.empaquetar(pts(33, vis=0.9), None, pts(21, 0.2), pts(478, 0.4), aspecto=2.0)
    assert out[F.IDX_HOMBRO_IZQ, 0] == pytest.approx(1.0)  # 0.5 * 2
    assert out[F.IDX_HOMBRO_IZQ, 1] == pytest.approx(0.25)
    assert np.all(out[F.INICIO_MANO_IZQ:F.INICIO_MANO_DER] == 0)
    assert out[F.INICIO_MANO_DER, 0] == pytest.approx(0.4)
    baja_vis = X.empaquetar(pts(33, vis=0.1), None, None, None, 1.0)
    assert np.all(baja_vis == 0)


def test_asignar_manos_por_cercania_a_la_pose():
    pose = [SimpleNamespace(x=0.5, y=0.5) for _ in range(33)]
    pose[15] = SimpleNamespace(x=0.8, y=0.6)  # muñeca izquierda de la persona
    pose[16] = SimpleNamespace(x=0.2, y=0.6)
    cerca_izq = [SimpleNamespace(x=0.78, y=0.6)]
    cerca_der = [SimpleNamespace(x=0.22, y=0.6)]
    izq, der = X.asignar_manos([cerca_der, cerca_izq], pose, ["Left", "Right"])
    assert izq is cerca_izq and der is cerca_der


def test_vocabulario_mvp():
    entradas = V.cargar()
    clases = V.clases_senas(entradas)
    assert clases[0] == "_FONDO" and len(clases) == len(set(clases))
    assert len(V.clases_dactilologia(entradas)) == 27
    assert "GRACIAS" in clases and "LETRA-A" not in clases


RAIZ = Path(__file__).resolve().parents[2]
RECURSOS = RAIZ / "unity" / "LSRD-AR" / "Assets" / "LSRD" / "Resources" / "LSRD"


@pytest.mark.parametrize("nombre", ["vocabulario_mvp.csv", "frases_mvp.csv", "emergencias.csv",
                                    "cobertura_diccionario_oficial.csv"])
def test_copias_del_vocabulario_en_unity_estan_al_dia(nombre):
    assert (RECURSOS / "Datos" / nombre).read_bytes() == (RAIZ / "vocabulario" / nombre).read_bytes(), \
        "Copie el vocabulario a Unity (menú LSRD > Copiar vocabulario o cp vocabulario/*.csv …/Resources/LSRD/Datos/)"


def test_grabaciones_de_simulacion_validas():
    rutas = sorted((RECURSOS / "Simulacion").glob("*.json"))
    assert rutas, "Ejecute scripts/exportar_simulacion.py"
    for r in rutas:
        m = D.cargar_json(r)
        assert m.metadatos["camara"]["alto"] > 0 and len(m.cuadros) >= 6
