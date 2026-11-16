"""Configuración del servidor por variables de entorno."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]


def _ruta(nombre: str, defecto: Path) -> Path:
    valor = os.environ.get(nombre)
    return Path(valor) if valor else defecto


@dataclass
class Config:
    vocabulario: Path = field(default_factory=lambda: _ruta("LSRD_VOCABULARIO", RAIZ / "vocabulario" / "vocabulario_mvp.csv"))
    frases: Path = field(default_factory=lambda: _ruta("LSRD_FRASES", RAIZ / "vocabulario" / "frases_mvp.csv"))
    emergencias: Path = field(default_factory=lambda: _ruta("LSRD_EMERGENCIAS", RAIZ / "vocabulario" / "emergencias.csv"))
    cobertura: Path = field(default_factory=lambda: _ruta("LSRD_COBERTURA", RAIZ / "vocabulario" / "cobertura_diccionario_oficial.csv"))
    datos: Path = field(default_factory=lambda: _ruta("LSRD_DATOS", RAIZ / "servidor" / "datos"))
    # Clave que la app envía en la cabecera X-Clave-LSRD. Sin clave, el servidor
    # solo acepta conexiones (modo desarrollo) y lo advierte en /salud.
    clave: str | None = field(default_factory=lambda: os.environ.get("LSRD_CLAVE") or None)
    # Traducción con modelo de lenguaje (opcional). Sin clave de API se usa
    # la memoria de traducción y la traducción literal.
    clave_anthropic: str | None = field(default_factory=lambda: os.environ.get("ANTHROPIC_API_KEY") or None)
    modelo_llm: str = field(default_factory=lambda: os.environ.get("LSRD_MODELO_LLM", "claude-sonnet-5-5"))
    tiempo_llm_s: float = field(default_factory=lambda: float(os.environ.get("LSRD_TIEMPO_LLM", "12")))
    # Voz a texto (opcional, requiere faster-whisper)
    modelo_whisper: str = field(default_factory=lambda: os.environ.get("LSRD_WHISPER", "small"))
    max_bytes_audio: int = 10 * 1024 * 1024
    max_bytes_muestra: int = 8 * 1024 * 1024
    max_bytes_reporte: int = 16 * 1024
