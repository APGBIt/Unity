"""Exportación de modelos a ONNX para Unity Inference Engine (Sentis).

Se exporta con opset 15, el rango que Inference Engine soporta en todas sus
versiones, y se verifica con ONNX Runtime que la salida coincide con PyTorch.
Junto al ``.onnx`` se escribe un ``.json`` con las clases y metadatos que la
app necesita.
"""

from __future__ import annotations

import datetime as _dt
import json
import warnings
from pathlib import Path
from typing import Optional

import numpy as np
import torch

from . import formato as F
from .modelo import EnvolturaExportacion

# Operadores ONNX que Inference Engine soporta y que este proyecto puede generar.
OPERADORES_PERMITIDOS = {
    "Abs", "Add", "Cast", "Clip", "Concat", "Constant", "ConstantOfShape", "Div", "Equal", "Erf",
    "Expand", "Gather", "Gemm", "Greater", "Identity", "LayerNormalization", "Less", "MatMul", "Max",
    "Min", "Mul", "Neg", "Pow", "ReduceMax", "ReduceMean", "ReduceSum", "Relu", "Reshape", "Shape",
    "Sigmoid", "Slice", "Softmax", "Sqrt", "Squeeze", "Sub", "Tanh", "Transpose", "Unsqueeze", "Where",
}

OPSET = 15


def _exportar(modelo: torch.nn.Module, ejemplo: torch.Tensor, ruta: Path, nombre_entrada: str) -> None:
    envoltura = EnvolturaExportacion(modelo).eval()
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        try:
            torch.onnx.export(
                envoltura, (ejemplo,), str(ruta),
                input_names=[nombre_entrada], output_names=["logits", "probs"],
                opset_version=OPSET, dynamo=False, do_constant_folding=True,
            )
        except TypeError:  # versiones de PyTorch sin el exportador clásico
            torch.onnx.export(
                envoltura, (ejemplo,), str(ruta),
                input_names=[nombre_entrada], output_names=["logits", "probs"],
                opset_version=18,
            )


def operadores(ruta: Path) -> set[str]:
    import onnx

    return {n.op_type for n in onnx.load(str(ruta)).graph.node}


def verificar(modelo: torch.nn.Module, ruta: Path, entradas: np.ndarray, nombre_entrada: str,
              tolerancia: float = 1e-4) -> float:
    """Compara PyTorch con ONNX Runtime; devuelve la diferencia máxima en probabilidades."""
    import onnxruntime as ort

    sesion = ort.InferenceSession(str(ruta), providers=["CPUExecutionProvider"])
    modelo.eval()
    peor = 0.0
    with torch.no_grad():
        for x in entradas:
            x1 = x[None].astype(np.float32)
            esperado = torch.softmax(modelo(torch.from_numpy(x1)), -1).numpy()
            obtenido = sesion.run(["probs"], {nombre_entrada: x1})[0]
            peor = max(peor, float(np.abs(esperado - obtenido).max()))
    if peor > tolerancia:
        raise AssertionError(f"ONNX difiere de PyTorch en {peor:.2e} (> {tolerancia:.0e})")
    return peor


def _escribir_json(ruta: Path, datos: dict) -> None:
    ruta.write_text(json.dumps(datos, ensure_ascii=False, indent=2), encoding="utf-8")


def exportar_senas(
    modelo: torch.nn.Module,
    clases: list[str],
    carpeta: Path,
    metricas: Optional[dict] = None,
    nombre: str = "senas",
    nota: str = "",
    entradas_prueba: Optional[np.ndarray] = None,
) -> dict:
    carpeta = Path(carpeta)
    carpeta.mkdir(parents=True, exist_ok=True)
    ruta = carpeta / f"{nombre}.onnx"
    ejemplo = torch.zeros(1, F.LONGITUD_SECUENCIA, F.N_PUNTOS, F.N_COORDENADAS)
    _exportar(modelo, ejemplo, ruta, "landmarks")

    extra = operadores(ruta) - OPERADORES_PERMITIDOS
    if extra:
        raise AssertionError(f"El modelo usa operadores no verificados para Inference Engine: {sorted(extra)}")

    if entradas_prueba is None:
        rng = np.random.default_rng(0)
        entradas_prueba = rng.uniform(0, 1, size=(4, F.LONGITUD_SECUENCIA, F.N_PUNTOS, 3)).astype(np.float32)
        entradas_prueba[1, :, F.INICIO_MANO_IZQ:F.INICIO_CARA] = 0.0  # sin manos
        entradas_prueba[2] = 0.0  # todo ausente
    diferencia = verificar(modelo, ruta, entradas_prueba, "landmarks")

    info = {
        "modelo": "senas",
        "formato": F.VERSION,
        "entrada": {"nombre": "landmarks", "forma": [1, F.LONGITUD_SECUENCIA, F.N_PUNTOS, F.N_COORDENADAS]},
        "salidas": ["logits", "probs"],
        "longitud_secuencia": F.LONGITUD_SECUENCIA,
        "clases": clases,
        "clase_fondo": "_FONDO" if "_FONDO" in clases else None,
        "opset": OPSET,
        "fecha_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
        "diferencia_onnx": diferencia,
        "metricas": _resumen_metricas(metricas),
        "nota": nota,
    }
    _escribir_json(carpeta / f"{nombre}_info.json", info)
    return info


def exportar_dactilologia(
    modelo: torch.nn.Module,
    clases: list[str],
    carpeta: Path,
    metricas: Optional[dict] = None,
    nombre: str = "dactilologia",
    nota: str = "",
) -> dict:
    carpeta = Path(carpeta)
    carpeta.mkdir(parents=True, exist_ok=True)
    ruta = carpeta / f"{nombre}.onnx"
    _exportar(modelo, torch.zeros(1, F.PUNTOS_MANO, 3), ruta, "mano")
    extra = operadores(ruta) - OPERADORES_PERMITIDOS
    if extra:
        raise AssertionError(f"El modelo usa operadores no verificados para Inference Engine: {sorted(extra)}")
    rng = np.random.default_rng(1)
    diferencia = verificar(modelo, ruta, rng.uniform(0, 1, size=(4, F.PUNTOS_MANO, 3)).astype(np.float32), "mano")
    info = {
        "modelo": "dactilologia",
        "formato": F.VERSION,
        "entrada": {"nombre": "mano", "forma": [1, F.PUNTOS_MANO, 3]},
        "salidas": ["logits", "probs"],
        "clases": clases,
        "opset": OPSET,
        "fecha_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
        "diferencia_onnx": diferencia,
        "metricas": _resumen_metricas(metricas),
        "nota": nota,
    }
    _escribir_json(carpeta / f"{nombre}_info.json", info)
    return info


def _resumen_metricas(m: Optional[dict]) -> Optional[dict]:
    if not m:
        return None
    return {k: m[k] for k in ("n", "exactitud", "exactitud_top3", "por_firmante", "tipo_medicion") if k in m}
