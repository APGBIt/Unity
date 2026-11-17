import base64
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / "ml"))

from lsrd_ml import datos as D  # noqa: E402
from lsrd_ml import formato as F  # noqa: E402


def _modulo(nombre):
    spec = importlib.util.spec_from_file_location(nombre, RAIZ / "ml" / "scripts" / f"{nombre}.py")
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def test_el_formato_de_la_app_web_esta_al_dia():
    r = subprocess.run([sys.executable, str(RAIZ / "ml" / "scripts" / "generar_javascript.py"), "--comprobar"],
                       capture_output=True, text=True)
    assert r.returncode == 0, r.stdout + r.stderr


def _paquete(consentimiento=True):
    rng = np.random.default_rng(1)
    sec = rng.normal(0.5, 0.1, size=(F.LONGITUD_SECUENCIA, F.N_PUNTOS, 3)).astype("<f4")
    b64 = base64.b64encode(sec.tobytes()).decode()
    return sec, {"tipo": "senas-rd/paquete", "version": 1, "formato": F.VERSION, "consentimiento_entrenamiento": consentimiento,
                 "ejemplos": [{"glosa": "HOLA", "sec_b64": b64, "duracion_ms": 900},
                              {"glosa": "SEÑORA-PEREZ", "sec_b64": b64},
                              {"glosa": "GRACIAS", "sec_b64": base64.b64encode(b"\x00" * 40).decode()}]}


def test_paquete_web_a_muestras_de_entrenamiento(tmp_path):
    m = _modulo("importar_paquete_web")
    sec, paquete = _paquete()
    r = m.convertir(json.loads(json.dumps(paquete)), "P007", tmp_path, {"HOLA", "GRACIAS"})
    assert r == {"escritas": 1, "fuera_de_vocabulario": 1, "invalidas": 1}
    muestras = D.cargar_carpeta(tmp_path)
    assert len(muestras) == 1 and muestras[0].glosa == "HOLA" and muestras[0].firmante == "P007"
    np.testing.assert_array_equal(muestras[0].cuadros, sec)
    assert muestras[0].tiempos_ms[-1] == pytest.approx(900.0)


def test_paquete_sin_autorizacion_no_entra(tmp_path):
    m = _modulo("importar_paquete_web")
    _, paquete = _paquete(consentimiento=False)
    with pytest.raises(PermissionError):
        m.convertir(paquete, "P007", tmp_path, {"HOLA"})
    assert not list(tmp_path.glob("*.npz"))
    with pytest.raises(ValueError):
        m.convertir({"tipo": "otra-cosa"}, "P007", tmp_path, {"HOLA"})
