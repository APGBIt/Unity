import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import segmentacion as G  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402


def _flujo(partes):
    cuadros, tiempos, t0 = [], [], 0.0
    for m in partes:
        cuadros.append(m.cuadros)
        tiempos.append(m.tiempos_ms - m.tiempos_ms[0] + t0)
        t0 = float(tiempos[-1][-1]) + 33.0
    return np.concatenate(cuadros), np.concatenate(tiempos)


def test_tres_senas_con_reposo_dan_tres_segmentos():
    rng = np.random.default_rng(1)
    f = S.nuevo_firmante(rng, "X")
    partes = [S.generar_muestra(rng, f, S.nueva_clase(rng, str(k)), fps=30, con_reposo=True) for k in range(3)]
    cuadros, tiempos = _flujo(partes)
    segs = G.segmentar_secuencia(cuadros, tiempos)
    assert len(segs) == 3
    # cada segmento cae dentro de su parte
    limites = np.cumsum([len(p.cuadros) for p in partes])
    for (ini, fin), lim in zip(segs, limites):
        assert fin <= tiempos[lim - 1]


def test_sena_estatica_se_corta_por_quietud_sin_bajar_las_manos():
    p = G.ParametrosSegmentacion()
    seg = G.Segmentador(p)
    cuadro = np.zeros((F.N_PUNTOS, 3), np.float32)
    cuadro[F.IDX_HOMBRO_IZQ] = [0.8, 0.6, 0]
    cuadro[F.IDX_HOMBRO_DER] = [0.5, 0.6, 0]
    cuadro[F.INICIO_MANO_DER:F.INICIO_CARA] = [0.45, 0.45, 0.01]
    emitidos = []
    anterior = None
    for k in range(60):  # 2 s quieta a 30 fps
        t = k * 33.3
        actual = cuadro.copy()
        if k < 8:  # la mano sube al principio
            actual[F.INICIO_MANO_DER:F.INICIO_CARA, 1] += 0.02 * (8 - k)
        activo, vel = G.medir_cuadro(actual, anterior, 0.0333 if anterior is not None else 0.0, p)
        r = seg.procesar(t, activo, vel)
        if r:
            emitidos.append(r)
        anterior = actual
    assert len(emitidos) == 1
    ini, fin = emitidos[0]
    assert fin - ini >= p.min_ms


def test_aparicion_breve_se_descarta():
    seg = G.Segmentador()
    salida = [seg.procesar(t, activo, 1.0) for t, activo in zip(range(0, 600, 33), [True] * 5 + [False] * 14)]
    assert all(s is None for s in salida)


def test_corte_forzado_en_firmado_continuo():
    p = G.ParametrosSegmentacion(max_ms=1000)
    seg = G.Segmentador(p)
    salida = [seg.procesar(float(t), True, 2.0) for t in range(0, 3500, 33)]
    cortes = [s for s in salida if s]
    assert len(cortes) == 3
    assert all(fin - ini >= 1000 for ini, fin in cortes)


def test_sin_hombros_solo_presencia():
    p = G.ParametrosSegmentacion()
    c = np.zeros((F.N_PUNTOS, 3), np.float32)
    c[F.INICIO_MANO_IZQ:F.INICIO_MANO_DER] = [0.5, 0.95, 0.0]
    activo, vel = G.medir_cuadro(c, None, 0.0, p)
    assert activo and vel == G.INFINITO


def test_mano_bajo_la_linea_de_reposo_no_cuenta():
    p = G.ParametrosSegmentacion()
    c = np.zeros((F.N_PUNTOS, 3), np.float32)
    c[F.IDX_HOMBRO_IZQ] = [0.7, 0.4, 0]
    c[F.IDX_HOMBRO_DER] = [0.4, 0.4, 0]
    c[F.INICIO_MANO_IZQ:F.INICIO_MANO_DER] = [0.6, 0.95, 0.0]  # 0.4 + 1.5*0.3 = 0.85 < 0.95
    assert G.medir_cuadro(c, None, 0.0, p)[0] is False
