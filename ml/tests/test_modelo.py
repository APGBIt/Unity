import sys
from pathlib import Path

import numpy as np
import pytest
import torch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from lsrd_ml import exportar as X  # noqa: E402
from lsrd_ml import formato as F  # noqa: E402
from lsrd_ml import remuestreo as R  # noqa: E402
from lsrd_ml import sintetico as S  # noqa: E402
from lsrd_ml.modelo import (ConfigModeloDactilologia, ConfigModeloSenas, ModeloDactilologia,  # noqa: E402
                            ModeloSenas)


@pytest.fixture(scope="module")
def entrada():
    rng = np.random.default_rng(0)
    m = S.generar_muestra(rng, S.nuevo_firmante(rng, "X"), S.nueva_clase(rng, "A"), fps=30)
    return torch.from_numpy(R.remuestrear(m.cuadros))[None]


@pytest.fixture(scope="module")
def modelo():
    torch.manual_seed(0)
    return ModeloSenas(ConfigModeloSenas(n_clases=7)).eval()


def test_formas(modelo, entrada):
    assert modelo(entrada).shape == (1, 7)


def test_invariante_a_traslacion_y_escala(modelo, entrada):
    x = entrada.clone()
    mascara = (x.abs().sum(-1, keepdim=True) > 0).float()
    movido = (x * 1.7 + torch.tensor([0.3, -0.1, 0.0])) * mascara
    with torch.no_grad():
        a, b = modelo(x), modelo(movido)
    torch.testing.assert_close(a, b, atol=2e-4, rtol=1e-4)


def test_entradas_degeneradas_no_producen_nan(modelo, entrada):
    vacio = torch.zeros_like(entrada)
    sin_hombros = entrada.clone()
    sin_hombros[:, :, F.INICIO_POSE:F.INICIO_MANO_IZQ] = 0
    sin_manos = entrada.clone()
    sin_manos[:, :, F.INICIO_MANO_IZQ:F.INICIO_CARA] = 0
    with torch.no_grad():
        for x in (vacio, sin_hombros, sin_manos):
            assert torch.isfinite(modelo(x)).all()


def test_exportacion_onnx_y_paridad(tmp_path, modelo):
    clases = [f"C{k}" for k in range(7)]
    info = X.exportar_senas(modelo, clases, tmp_path)
    assert info["diferencia_onnx"] < 1e-4
    assert X.operadores(tmp_path / "senas.onnx") <= X.OPERADORES_PERMITIDOS
    import onnx

    m = onnx.load(str(tmp_path / "senas.onnx"))
    assert [o.version for o in m.opset_import if o.domain in ("", "ai.onnx")] == [X.OPSET]
    assert [i.name for i in m.graph.input] == ["landmarks"]
    assert [o.name for o in m.graph.output] == ["logits", "probs"]


def test_dactilologia_invariante_a_posicion_y_exporta(tmp_path):
    torch.manual_seed(0)
    md = ModeloDactilologia(ConfigModeloDactilologia(n_clases=5)).eval()
    rng = np.random.default_rng(1)
    mano = torch.from_numpy(rng.uniform(0.3, 0.6, size=(1, 21, 3)).astype(np.float32))
    with torch.no_grad():
        torch.testing.assert_close(md(mano), md(mano * 2.0 + 0.1), atol=1e-4, rtol=1e-4)
    info = X.exportar_dactilologia(md, [f"L{k}" for k in range(5)], tmp_path)
    assert info["diferencia_onnx"] < 1e-4
