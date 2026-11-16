"""Reportes de retroalimentación: traducciones incorrectas, señas no reconocidas, avatar.

Un reporte es texto que la persona decide enviar; nunca incluye audio ni video.
Queda en ``pendiente`` hasta que alguien del equipo lingüístico lo revisa. Los
reportes **no cambian** el vocabulario, la memoria de traducción ni los datos de
entrenamiento: eso lo hace una persona después de revisarlos.
"""

from __future__ import annotations

import datetime as _dt
import json
import re
import uuid
from collections import Counter
from pathlib import Path

TIPOS = ("traduccion_incorrecta", "sena_no_reconocida", "avatar_incorrecto", "otro")
DIRECCIONES = ("senas_a_texto", "texto_a_senas", "")
ESTADOS = ("pendiente", "aceptado", "descartado")
_ID = re.compile(r"^[A-Za-z0-9_-]{6,64}$")


class ReporteInvalido(ValueError):
    pass


def _texto(d: dict, clave: str, maximo: int) -> str:
    v = d.get(clave, "")
    if not isinstance(v, str):
        raise ReporteInvalido(f"«{clave}» debe ser texto")
    if len(v) > maximo:
        raise ReporteInvalido(f"«{clave}» supera los {maximo} caracteres")
    return v.strip()


class AlmacenReportes:
    def __init__(self, carpeta: Path) -> None:
        self.carpeta = Path(carpeta) / "retroalimentacion"
        self.carpeta.mkdir(parents=True, exist_ok=True)

    def guardar(self, d: dict) -> str:
        if not isinstance(d, dict):
            raise ReporteInvalido("El reporte debe ser un objeto JSON")
        tipo = d.get("tipo")
        if tipo not in TIPOS:
            raise ReporteInvalido(f"Tipo desconocido: {tipo!r}")
        direccion = d.get("direccion", "")
        if direccion not in DIRECCIONES:
            raise ReporteInvalido(f"Dirección desconocida: {direccion!r}")
        glosas = d.get("glosas", [])
        if not (isinstance(glosas, list) and len(glosas) <= 60 and all(isinstance(g, str) and len(g) <= 60 for g in glosas)):
            raise ReporteInvalido("«glosas» debe ser una lista de hasta 60 textos")
        ident = str(d.get("id") or uuid.uuid4().hex)
        if not _ID.match(ident):
            raise ReporteInvalido("Identificador inválido")
        reporte = {
            "id": ident,
            "tipo": tipo,
            "direccion": direccion,
            "texto_mostrado": _texto(d, "texto_mostrado", 500),
            "glosas": glosas,
            "correccion": _texto(d, "correccion", 500),
            "fuente": _texto(d, "fuente", 40),
            "modelo": _texto(d, "modelo", 200),
            "version_app": _texto(d, "version_app", 40),
            "fecha_app_utc": _texto(d, "fecha_utc", 40),
            "recibido_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
            "estado": "pendiente",
            "revision": None,
        }
        if not (reporte["texto_mostrado"] or reporte["glosas"] or reporte["correccion"]):
            raise ReporteInvalido("El reporte está vacío")
        (self.carpeta / f"{ident}.json").write_text(json.dumps(reporte, ensure_ascii=False, indent=2), encoding="utf-8")
        return ident

    def _leer(self) -> list[dict]:
        reportes = []
        for ruta in sorted(self.carpeta.glob("*.json")):
            try:
                reportes.append(json.loads(ruta.read_text(encoding="utf-8")))
            except json.JSONDecodeError:
                continue
        return sorted(reportes, key=lambda r: r.get("recibido_utc", ""), reverse=True)

    def listar(self, estado: str | None = None, limite: int = 200) -> list[dict]:
        return [r for r in self._leer() if estado is None or r.get("estado") == estado][:limite]

    def revisar(self, ident: str, estado: str, revisor: str, nota: str = "") -> dict:
        """Registra la decisión de quien valida. No modifica ningún otro dato."""
        if estado not in ("aceptado", "descartado"):
            raise ReporteInvalido("La revisión debe ser «aceptado» o «descartado»")
        if not revisor.strip():
            raise ReporteInvalido("Falta el nombre de quien revisa")
        ruta = self.carpeta / f"{ident}.json"
        if not _ID.match(ident) or not ruta.exists():
            raise KeyError(ident)
        r = json.loads(ruta.read_text(encoding="utf-8"))
        r["estado"] = estado
        r["revision"] = {"revisor": revisor.strip()[:80], "nota": nota.strip()[:500],
                         "fecha_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds")}
        ruta.write_text(json.dumps(r, ensure_ascii=False, indent=2), encoding="utf-8")
        return r

    def resumen(self) -> dict:
        reportes = self._leer()
        glosas = Counter(g for r in reportes if r.get("estado") != "descartado" for g in r.get("glosas", []))
        return {
            "total": len(reportes),
            "por_estado": dict(Counter(r.get("estado", "") for r in reportes)),
            "por_tipo": dict(Counter(r.get("tipo", "") for r in reportes)),
            "glosas_mas_reportadas": [{"glosa": g, "reportes": n} for g, n in glosas.most_common(20)],
        }
