"""Recepción y almacenamiento de muestras grabadas con el modo recolección de la app.

Las muestras contienen solo puntos clave (no video) y un perfil seudónimo del
firmante. Se rechazan las que no traen consentimiento explícito.
"""

from __future__ import annotations

import base64
import json
import re
import uuid
from collections import Counter, defaultdict
from pathlib import Path

FORMATO = "LSRD-LM v1"
N_PUNTOS = 78
_SEGURO = re.compile(r"[^A-Za-z0-9ÑñÁÉÍÓÚáéíóú_\-]")


class MuestraInvalida(ValueError):
    pass


def _seguro(texto: str, defecto: str) -> str:
    limpio = _SEGURO.sub("_", texto or "")[:60]
    return limpio or defecto


def validar(d: dict, glosas_validas: set[str]) -> None:
    if d.get("formato") != FORMATO:
        raise MuestraInvalida(f"Formato {d.get('formato')!r}; se esperaba {FORMATO!r}")
    consentimiento = d.get("consentimiento") or {}
    if not consentimiento.get("puntos_clave"):
        raise MuestraInvalida("La muestra no tiene consentimiento para guardar puntos clave")
    glosa = d.get("glosa")
    if glosa not in glosas_validas:
        raise MuestraInvalida(f"Glosa desconocida: {glosa!r}")
    forma = d.get("forma")
    if not (isinstance(forma, list) and len(forma) == 3 and forma[1:] == [N_PUNTOS, 3] and 1 <= forma[0] <= 900):
        raise MuestraInvalida(f"Forma inválida: {forma}")
    tiempos = d.get("tiempos_ms")
    if not isinstance(tiempos, list) or len(tiempos) != forma[0]:
        raise MuestraInvalida("tiempos_ms no coincide con el número de cuadros")
    try:
        datos = base64.b64decode(d.get("cuadros_b64", ""), validate=True)
    except Exception as e:  # noqa: BLE001
        raise MuestraInvalida("cuadros_b64 no es base64 válido") from e
    if len(datos) != forma[0] * N_PUNTOS * 3 * 4:
        raise MuestraInvalida("El tamaño de cuadros_b64 no coincide con la forma")


class Almacen:
    def __init__(self, carpeta: Path) -> None:
        self.carpeta = Path(carpeta) / "recolecciones"
        self.carpeta.mkdir(parents=True, exist_ok=True)

    def guardar(self, d: dict, glosas_validas: set[str]) -> str:
        validar(d, glosas_validas)
        ident = _seguro(str(d.get("id") or uuid.uuid4().hex), uuid.uuid4().hex)
        firmante = _seguro(str(d.get("firmante", "")), "anonimo")
        destino = self.carpeta / firmante
        destino.mkdir(parents=True, exist_ok=True)
        ruta = destino / f"{_seguro(d['glosa'], 'glosa')}__{ident}.json"
        ruta.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
        return ident

    def resumen(self) -> dict:
        por_glosa: Counter = Counter()
        firmantes_por_glosa: dict[str, set] = defaultdict(set)
        por_firmante: Counter = Counter()
        for ruta in self.carpeta.rglob("*.json"):
            try:
                glosa, _ = ruta.stem.split("__", 1)
            except ValueError:
                continue
            firmante = ruta.parent.name
            por_glosa[glosa] += 1
            por_firmante[firmante] += 1
            firmantes_por_glosa[glosa].add(firmante)
        return {
            "total": sum(por_glosa.values()),
            "firmantes": len(por_firmante),
            "por_firmante": dict(por_firmante),
            "por_glosa": {g: {"muestras": n, "firmantes": len(firmantes_por_glosa[g])} for g, n in por_glosa.items()},
        }


class AvatarAlmacen:
    """Grabaciones de referencia que anima el avatar (datos/avatar/<GLOSA>.json).

    Solo se publican muestras cuyo firmante consintió el uso para el avatar
    (``consentimiento.avatar = true``). Use ``publicar_avatar.py`` para agregarlas.
    """

    def __init__(self, carpeta: Path) -> None:
        self.carpeta = Path(carpeta) / "avatar"
        self.carpeta.mkdir(parents=True, exist_ok=True)

    def indice(self) -> list[dict]:
        import hashlib

        salida = []
        for ruta in sorted(self.carpeta.glob("*.json")):
            version = hashlib.sha1(ruta.read_bytes()).hexdigest()[:12]
            salida.append({"glosa": ruta.stem, "version": version})
        return salida

    def leer(self, glosa: str) -> str | None:
        if _seguro(glosa, "") != glosa:
            return None
        ruta = self.carpeta / f"{glosa}.json"
        return ruta.read_text(encoding="utf-8") if ruta.exists() else None

    def publicar(self, d: dict, glosas_validas: set[str]) -> Path:
        validar(d, glosas_validas)
        if not (d.get("consentimiento") or {}).get("avatar"):
            raise MuestraInvalida("La persona no dio consentimiento para usar sus movimientos en el avatar")
        ruta = self.carpeta / f"{_seguro(d['glosa'], 'glosa')}.json"
        ruta.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
        return ruta
