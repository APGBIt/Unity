"""Modelos de reconocimiento: señas (secuencias) y dactilología (forma de la mano).

La normalización vive dentro del modelo para que la app solo tenga que empaquetar
los puntos crudos en el formato LSRD-LM v1. Así no hay dos implementaciones de la
normalización (Python y C#) que puedan desincronizarse.

Todo se escribe con operaciones básicas (MatMul, Softmax, ReduceSum, Where…)
para exportar a ONNX opset 15 y correr en Unity Inference Engine.
"""

from __future__ import annotations

import math
from dataclasses import asdict, dataclass

import torch
from torch import nn

from . import formato as F

EPS = 1e-6


def _norma2d(v: torch.Tensor) -> torch.Tensor:
    """Norma euclidiana de las dos primeras coordenadas (sin ``linalg`` para exportar limpio)."""
    return torch.sqrt(v[..., 0] * v[..., 0] + v[..., 1] * v[..., 1] + EPS)


class NormalizacionLSRD(nn.Module):
    """``[B, T, N, 3]`` crudo (0 = ausente) → características por cuadro ``[B, T, D]`` y peso de cuadro ``[B, T]``.

    1. Centra en el punto medio de los hombros y escala por el ancho de hombros
       (promedios de la secuencia; si no hay hombros, usa el centro de los puntos visibles).
    2. Agrega la forma local de cada mano (relativa a la muñeca, escalada por el tamaño de la mano).
    3. Agrega velocidades cuadro a cuadro y banderas de presencia por grupo.
    """

    def __init__(self) -> None:
        super().__init__()
        self.dim_salida = F.N_PUNTOS * 3 * 2 + F.PUNTOS_MANO * 3 * 2 + 4

    def forward(self, x: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        b, t, n, _ = x.shape
        presente = (x.abs().sum(-1) > 0).float()  # [B, T, N]

        hi = x[:, :, F.IDX_HOMBRO_IZQ, :]
        hd = x[:, :, F.IDX_HOMBRO_DER, :]
        ph = presente[:, :, F.IDX_HOMBRO_IZQ] * presente[:, :, F.IDX_HOMBRO_DER]  # [B, T]
        n_h = ph.sum(1)  # [B]
        hay_h = (n_h > 0).float()
        n_h = torch.clamp(n_h, min=1.0)

        centro_f = (hi[..., :2] + hd[..., :2]) * 0.5  # [B, T, 2]
        escala_f = _norma2d(hi - hd)  # [B, T]
        centro_h = (centro_f * ph.unsqueeze(-1)).sum(1) / n_h.unsqueeze(-1)  # [B, 2]
        escala_h = (escala_f * ph).sum(1) / n_h  # [B]

        n_p = torch.clamp(presente.sum((1, 2)), min=1.0)  # [B]
        centro_p = (x[..., :2] * presente.unsqueeze(-1)).sum((1, 2)) / n_p.unsqueeze(-1)  # [B, 2]

        centro = hay_h.unsqueeze(-1) * centro_h + (1.0 - hay_h.unsqueeze(-1)) * centro_p
        escala = hay_h * escala_h + (1.0 - hay_h) * 0.3
        escala = torch.clamp(escala, min=1e-3)

        c = centro.view(b, 1, 1, 2)
        s = escala.view(b, 1, 1, 1)
        xy = (x[..., :2] - c) / s
        z = x[..., 2:3] / s
        g = torch.cat([xy, z], dim=-1) * presente.unsqueeze(-1)  # [B, T, N, 3]

        p_mi = presente[:, :, F.IDX_MUNECA_IZQ]
        p_md = presente[:, :, F.IDX_MUNECA_DER]
        local_i = self._mano_local(g[:, :, F.INICIO_MANO_IZQ:F.INICIO_MANO_DER, :]) * p_mi.view(b, t, 1, 1)
        local_d = self._mano_local(g[:, :, F.INICIO_MANO_DER:F.INICIO_CARA, :]) * p_md.view(b, t, 1, 1)

        ambos = presente[:, 1:, :] * presente[:, :-1, :]
        vel = (g[:, 1:] - g[:, :-1]) * ambos.unsqueeze(-1)
        vel = torch.cat([torch.zeros_like(g[:, :1]), vel], dim=1)

        grupos = torch.stack(
            [
                presente[:, :, F.IDX_HOMBRO_IZQ],
                p_mi,
                p_md,
                presente[:, :, F.IDX_CARA_REFERENCIA],
            ],
            dim=-1,
        )
        caracteristicas = torch.cat(
            [
                g.reshape(b, t, -1),
                local_i.reshape(b, t, -1),
                local_d.reshape(b, t, -1),
                vel.reshape(b, t, -1),
                grupos,
            ],
            dim=-1,
        )
        peso = torch.clamp(p_mi + p_md, max=1.0)  # cuadros con alguna mano
        return caracteristicas, peso

    @staticmethod
    def _mano_local(mano: torch.Tensor) -> torch.Tensor:
        muneca = mano[:, :, F.MANO_MUNECA:F.MANO_MUNECA + 1, :]
        tam = torch.clamp(_norma2d(mano[:, :, F.MANO_BASE_MEDIO, :] - mano[:, :, F.MANO_MUNECA, :]), min=1e-3)
        return (mano - muneca) / tam.unsqueeze(-1).unsqueeze(-1)


class AtencionMultiCabeza(nn.Module):
    def __init__(self, d: int, cabezas: int, dropout: float) -> None:
        super().__init__()
        assert d % cabezas == 0
        self.h = cabezas
        self.dk = d // cabezas
        self.qkv = nn.Linear(d, 3 * d)
        self.salida = nn.Linear(d, d)
        self.drop = nn.Dropout(dropout)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        b, t, d = x.shape
        qkv = self.qkv(x).reshape(b, t, 3, self.h, self.dk).permute(2, 0, 3, 1, 4)
        q, k, v = qkv[0], qkv[1], qkv[2]  # [B, H, T, dk]
        att = torch.matmul(q, k.transpose(-2, -1)) * (1.0 / math.sqrt(self.dk))
        att = self.drop(torch.softmax(att, dim=-1))
        y = torch.matmul(att, v).transpose(1, 2).reshape(b, t, d)
        return self.salida(y)


class BloqueTransformer(nn.Module):
    def __init__(self, d: int, cabezas: int, ff: int, dropout: float) -> None:
        super().__init__()
        self.n1 = nn.LayerNorm(d)
        self.att = AtencionMultiCabeza(d, cabezas, dropout)
        self.n2 = nn.LayerNorm(d)
        self.ff = nn.Sequential(nn.Linear(d, ff), nn.ReLU(), nn.Dropout(dropout), nn.Linear(ff, d))
        self.drop = nn.Dropout(dropout)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        x = x + self.drop(self.att(self.n1(x)))
        return x + self.drop(self.ff(self.n2(x)))


@dataclass
class ConfigModeloSenas:
    n_clases: int
    d: int = 192
    cabezas: int = 4
    capas: int = 3
    ff: int = 384
    dropout: float = 0.15
    longitud: int = F.LONGITUD_SECUENCIA

    def a_dict(self) -> dict:
        return asdict(self)


class ModeloSenas(nn.Module):
    """Clasificador de señas aisladas a partir de ``[B, 32, 78, 3]`` puntos crudos."""

    def __init__(self, cfg: ConfigModeloSenas) -> None:
        super().__init__()
        self.cfg = cfg
        self.norm = NormalizacionLSRD()
        self.entrada = nn.Sequential(
            nn.Linear(self.norm.dim_salida, cfg.d),
            nn.LayerNorm(cfg.d),
            nn.ReLU(),
            nn.Dropout(cfg.dropout),
            nn.Linear(cfg.d, cfg.d),
        )
        self.posicion = nn.Parameter(torch.zeros(1, cfg.longitud, cfg.d))
        nn.init.normal_(self.posicion, std=0.02)
        self.bloques = nn.ModuleList(
            [BloqueTransformer(cfg.d, cfg.cabezas, cfg.ff, cfg.dropout) for _ in range(cfg.capas)]
        )
        self.norm_final = nn.LayerNorm(cfg.d)
        self.cabeza = nn.Sequential(nn.Dropout(cfg.dropout), nn.Linear(cfg.d, cfg.n_clases))

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        feats, peso = self.norm(x)
        h = self.entrada(feats) + self.posicion
        for bloque in self.bloques:
            h = bloque(h)
        h = self.norm_final(h)
        # promedio ponderado por cuadros con manos; si ninguno tiene manos, promedio simple
        total = peso.sum(1, keepdim=True)  # [B, 1]
        sin_manos = (total < 0.5).float()
        peso = peso + sin_manos  # todos los cuadros pesan 1
        total = peso.sum(1, keepdim=True)
        pooled = (h * peso.unsqueeze(-1)).sum(1) / total
        return self.cabeza(pooled)


class EnvolturaExportacion(nn.Module):
    """Agrega la salida ``probs`` (softmax) al modelo para la app."""

    def __init__(self, modelo: nn.Module) -> None:
        super().__init__()
        self.modelo = modelo

    def forward(self, x: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        logits = self.modelo(x)
        return logits, torch.softmax(logits, dim=-1)


# ----------------------------------------------------------------------------
# Dactilología
# ----------------------------------------------------------------------------


@dataclass
class ConfigModeloDactilologia:
    n_clases: int
    oculto: int = 256
    dropout: float = 0.2

    def a_dict(self) -> dict:
        return asdict(self)


class ModeloDactilologia(nn.Module):
    """Clasifica la forma de una mano ``[B, 21, 3]``.

    La app entrega la mano activa ya "canonizada" como mano derecha (si es la
    izquierda, invierte x). Aquí se centra en la muñeca y se escala por el
    tamaño de la mano; la orientación se conserva porque distingue letras.
    """

    def __init__(self, cfg: ConfigModeloDactilologia) -> None:
        super().__init__()
        self.cfg = cfg
        self.red = nn.Sequential(
            nn.Linear(F.PUNTOS_MANO * 3, cfg.oculto),
            nn.LayerNorm(cfg.oculto),
            nn.ReLU(),
            nn.Dropout(cfg.dropout),
            nn.Linear(cfg.oculto, cfg.oculto // 2),
            nn.ReLU(),
            nn.Dropout(cfg.dropout),
            nn.Linear(cfg.oculto // 2, cfg.n_clases),
        )

    def forward(self, mano: torch.Tensor) -> torch.Tensor:
        muneca = mano[:, F.MANO_MUNECA:F.MANO_MUNECA + 1, :]
        tam = torch.clamp(_norma2d(mano[:, F.MANO_BASE_MEDIO, :] - mano[:, F.MANO_MUNECA, :]), min=1e-3)
        local = (mano - muneca) / tam.view(-1, 1, 1)
        return self.red(local.reshape(mano.shape[0], -1))


def contar_parametros(modelo: nn.Module) -> int:
    return sum(p.numel() for p in modelo.parameters())
