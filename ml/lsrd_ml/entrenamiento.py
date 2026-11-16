"""Entrenamiento y evaluación de los modelos."""

from __future__ import annotations

import time
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from typing import Callable, Optional

import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader

from .datos import DatasetDactilologia, DatasetSenas, Muestra
from .modelo import ConfigModeloDactilologia, ConfigModeloSenas, ModeloDactilologia, ModeloSenas


@dataclass
class ConfigEntrenamiento:
    epocas: int = 60
    lote: int = 64
    tasa: float = 1e-3
    decaimiento: float = 0.05
    suavizado: float = 0.1
    paciencia: int = 12
    semilla: int = 0
    hilos: int = 0  # 0 = lo que decida PyTorch


@dataclass
class Resultado:
    modelo: nn.Module
    clases: list[str]
    historial: list[dict] = field(default_factory=list)
    mejor_epoca: int = 0
    metricas_validacion: dict = field(default_factory=dict)


def _fijar_semillas(semilla: int) -> None:
    torch.manual_seed(semilla)
    np.random.seed(semilla)


@torch.no_grad()
def predecir(modelo: nn.Module, cargador: DataLoader) -> tuple[np.ndarray, np.ndarray]:
    modelo.eval()
    probs, etiquetas = [], []
    for x, y in cargador:
        probs.append(torch.softmax(modelo(x), -1).numpy())
        etiquetas.append(np.asarray(y))
    if not probs:
        return np.zeros((0, 0)), np.zeros(0, dtype=int)
    return np.concatenate(probs), np.concatenate(etiquetas)


def metricas(probs: np.ndarray, etiquetas: np.ndarray, clases: list[str]) -> dict:
    if len(etiquetas) == 0:
        return {"n": 0}
    pred = probs.argmax(1)
    top3 = np.argsort(-probs, axis=1)[:, :3]
    exact = float((pred == etiquetas).mean())
    exact3 = float(np.mean([e in t for e, t in zip(etiquetas, top3)]))
    recall = {}
    for k, c in enumerate(clases):
        sel = etiquetas == k
        if sel.any():
            recall[c] = float((pred[sel] == k).mean())
    confusiones = Counter(
        (clases[e], clases[p]) for e, p in zip(etiquetas, pred) if e != p
    ).most_common(10)
    return {
        "n": int(len(etiquetas)),
        "exactitud": exact,
        "exactitud_top3": exact3,
        "recall_por_clase": recall,
        "peores_clases": sorted(recall.items(), key=lambda kv: kv[1])[:10],
        "confusiones_frecuentes": [{"real": a, "predicha": b, "veces": n} for (a, b), n in confusiones],
    }


def _ciclo(
    modelo: nn.Module,
    ds_ent,
    ds_val,
    clases: list[str],
    cfg: ConfigEntrenamiento,
    informar: Optional[Callable[[dict], None]] = None,
) -> Resultado:
    if cfg.hilos:
        torch.set_num_threads(cfg.hilos)
    cargador = DataLoader(ds_ent, batch_size=cfg.lote, shuffle=True, drop_last=len(ds_ent) > cfg.lote)
    cargador_val = DataLoader(ds_val, batch_size=256, shuffle=False)
    opt = torch.optim.AdamW(modelo.parameters(), lr=cfg.tasa, weight_decay=cfg.decaimiento)
    pasos = max(1, cfg.epocas * len(cargador))
    sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=cfg.tasa, total_steps=pasos, pct_start=0.1)
    perdida_fn = nn.CrossEntropyLoss(label_smoothing=cfg.suavizado)

    mejor, mejor_estado, mejor_epoca, sin_mejora = -1.0, None, 0, 0
    res = Resultado(modelo=modelo, clases=clases)
    for epoca in range(1, cfg.epocas + 1):
        modelo.train()
        t0 = time.time()
        total, n = 0.0, 0
        for x, y in cargador:
            opt.zero_grad()
            perdida = perdida_fn(modelo(x), y)
            perdida.backward()
            nn.utils.clip_grad_norm_(modelo.parameters(), 1.0)
            opt.step()
            sched.step()
            total += perdida.item() * len(y)
            n += len(y)
        probs, etiquetas = predecir(modelo, cargador_val)
        m = metricas(probs, etiquetas, clases)
        fila = {
            "epoca": epoca,
            "perdida": total / max(1, n),
            "exactitud_val": m.get("exactitud", 0.0),
            "top3_val": m.get("exactitud_top3", 0.0),
            "segundos": round(time.time() - t0, 1),
        }
        res.historial.append(fila)
        if informar:
            informar(fila)
        if fila["exactitud_val"] > mejor:
            mejor, mejor_epoca, sin_mejora = fila["exactitud_val"], epoca, 0
            mejor_estado = {k: v.detach().clone() for k, v in modelo.state_dict().items()}
            res.metricas_validacion = m
        else:
            sin_mejora += 1
            if sin_mejora >= cfg.paciencia:
                break
    if mejor_estado is not None:
        modelo.load_state_dict(mejor_estado)
    res.mejor_epoca = mejor_epoca
    modelo.eval()
    return res


def entrenar_senas(
    entrenamiento: list[Muestra],
    validacion: list[Muestra],
    clases: list[str],
    cfg: Optional[ConfigEntrenamiento] = None,
    cfg_modelo: Optional[ConfigModeloSenas] = None,
    informar: Optional[Callable[[dict], None]] = None,
) -> Resultado:
    cfg = cfg or ConfigEntrenamiento()
    _fijar_semillas(cfg.semilla)
    modelo = ModeloSenas(cfg_modelo or ConfigModeloSenas(n_clases=len(clases)))
    ds_ent = DatasetSenas(entrenamiento, clases, aumentar=True, semilla=cfg.semilla)
    ds_val = DatasetSenas(validacion, clases, aumentar=False)
    if len(ds_ent) == 0 or len(ds_val) == 0:
        raise ValueError("No hay muestras de entrenamiento o validación para estas clases")
    return _ciclo(modelo, ds_ent, ds_val, clases, cfg, informar)


def entrenar_dactilologia(
    entrenamiento: list[Muestra],
    validacion: list[Muestra],
    clases: list[str],
    cfg: Optional[ConfigEntrenamiento] = None,
    informar: Optional[Callable[[dict], None]] = None,
) -> Resultado:
    cfg = cfg or ConfigEntrenamiento(epocas=40, lote=256, tasa=2e-3)
    _fijar_semillas(cfg.semilla)
    modelo = ModeloDactilologia(ConfigModeloDactilologia(n_clases=len(clases)))
    ds_ent = DatasetDactilologia(entrenamiento, clases, aumentar=True, semilla=cfg.semilla)
    ds_val = DatasetDactilologia(validacion, clases, aumentar=False)
    if len(ds_ent) == 0 or len(ds_val) == 0:
        raise ValueError("No hay muestras de letras para entrenar o validar")
    return _ciclo(modelo, ds_ent, ds_val, clases, cfg, informar)


def evaluar(modelo: nn.Module, muestras: list[Muestra], clases: list[str], dactilologia: bool = False) -> dict:
    """Métricas globales y por firmante sobre muestras nunca vistas."""
    ds_cls = DatasetDactilologia if dactilologia else DatasetSenas
    global_ = metricas(*predecir(modelo, DataLoader(ds_cls(muestras, clases, aumentar=False), batch_size=256)), clases)
    por_firmante = defaultdict(list)
    for m in muestras:
        por_firmante[m.firmante].append(m)
    global_["por_firmante"] = {
        f: metricas(*predecir(modelo, DataLoader(ds_cls(ms, clases, aumentar=False), batch_size=256)), clases).get("exactitud")
        for f, ms in sorted(por_firmante.items())
    }
    return global_
