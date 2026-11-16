"""Servidor de la app LSRD: traducción, voz a texto y recepción de grabaciones.

Ejecutar:
    uvicorn app.main:app --host 0.0.0.0 --port 8000

Variables de entorno: ver ``app/config.py`` y ``servidor/README.md``.
"""

from __future__ import annotations

import hashlib
import hmac
import logging
from pathlib import Path
from typing import Optional

from fastapi import Depends, FastAPI, File, Header, HTTPException, Request, UploadFile
from pydantic import BaseModel, Field

from .config import Config
from .memoria import Memoria
from .recolecciones import Almacen, AvatarAlmacen, MuestraInvalida
from .retroalimentacion import AlmacenReportes, ReporteInvalido
from .traductor import Traductor
from .vocabulario import Vocabulario
from .voz import Transcriptor, VozNoDisponible

VERSION = "0.2.0"
log = logging.getLogger("lsrd")


class PeticionTexto(BaseModel):
    texto: str = Field(..., max_length=500)


class PeticionGlosas(BaseModel):
    glosas: list[str] = Field(..., max_length=60)


class PeticionRevision(BaseModel):
    estado: str
    revisor: str = Field(..., max_length=80)
    nota: str = Field("", max_length=500)


def _leer_cobertura(ruta: Path) -> dict[str, dict]:
    if not Path(ruta).exists():
        return {}
    import csv

    with open(ruta, encoding="utf-8", newline="") as fh:
        return {f["glosa"]: f for f in csv.DictReader(fh)}


def crear_app(cfg: Optional[Config] = None, llm=None, transcriptor: Optional[Transcriptor] = None) -> FastAPI:
    cfg = cfg or Config()
    vocab = Vocabulario(cfg.vocabulario)
    memoria = Memoria(cfg.frases)
    if llm is None and cfg.clave_anthropic:
        try:
            from .llm import TraductorLLM

            llm = TraductorLLM(cfg, vocab, memoria)
        except Exception as e:  # noqa: BLE001
            log.warning("No se pudo iniciar la traducción con LLM: %s", e)
            llm = None
    traductor = Traductor(vocab, memoria, llm)
    transcriptor = transcriptor or Transcriptor(cfg.modelo_whisper)
    almacen = Almacen(cfg.datos)
    avatar = AvatarAlmacen(cfg.datos)
    reportes = AlmacenReportes(cfg.datos)
    emergencias = Memoria(cfg.emergencias) if Path(cfg.emergencias).exists() else None
    cobertura = _leer_cobertura(cfg.cobertura)
    glosas_validas = set(vocab.por_glosa)
    version_vocab = hashlib.sha256(Path(cfg.vocabulario).read_bytes()).hexdigest()[:12]

    app = FastAPI(title="Servidor LSRD", version=VERSION,
                  description="Traducción LSRD ↔ español dominicano para la app de realidad aumentada.")

    def autorizar(x_clave_lsrd: Optional[str] = Header(default=None)) -> None:
        if cfg.clave and not (x_clave_lsrd and hmac.compare_digest(x_clave_lsrd, cfg.clave)):
            raise HTTPException(status_code=401, detail="Clave inválida o ausente (cabecera X-Clave-LSRD)")

    protegido = [Depends(autorizar)]

    @app.get("/salud")
    def salud() -> dict:
        return {
            "estado": "ok",
            "version": VERSION,
            "traduccion_llm": llm is not None,
            "voz_a_texto": Transcriptor.disponible(),
            "clave_configurada": cfg.clave is not None,
            "vocabulario": len(vocab.entradas),
            "version_vocabulario": version_vocab,
            "frases_validadas": len(memoria.validadas),
        }

    @app.get("/v1/vocabulario", dependencies=protegido)
    def vocabulario() -> dict:
        return {"version": version_vocab, "entradas": [e.a_dict() for e in vocab.entradas]}

    @app.get("/v1/frases", dependencies=protegido)
    def frases(direccion: Optional[str] = None, solo_rapidas: bool = False) -> dict:
        lista = memoria.rapidas(direccion) if solo_rapidas else [
            f for f in memoria.frases if direccion is None or f.direccion == direccion]
        return {"frases": [f.a_dict() for f in lista]}

    @app.get("/v1/emergencias", dependencies=protegido)
    def frases_emergencia(direccion: Optional[str] = None) -> dict:
        lista = emergencias.frases if emergencias else []
        return {"frases": [f.a_dict() for f in lista if direccion is None or f.direccion == direccion]}

    @app.get("/v1/diccionario", dependencies=protegido)
    def diccionario(buscar: str = "", categoria: Optional[str] = None) -> dict:
        """Fichas del diccionario de la app: vocabulario + referencia oficial + si hay animación."""
        from .vocabulario import normalizar, sin_tildes

        consulta = sin_tildes(normalizar(buscar))
        con_avatar = {i["glosa"] for i in avatar.indice()}
        fichas = []
        for e in vocab.entradas:
            if e.tipo == "fondo" or (categoria and e.categoria != categoria):
                continue
            terminos = [e.espanol, e.glosa.replace("-", " "), *e.sinonimos]
            if consulta and not any(consulta in sin_tildes(normalizar(t)) for t in terminos):
                continue
            c = cobertura.get(e.glosa, {})
            ficha = e.a_dict()
            ficha.update({
                "referencia_oficial": {"titulo": c.get("titulo_oficial", ""), "url": c.get("url", "")}
                if c.get("resultado") == "encontrada" else None,
                "tiene_animacion": e.glosa in con_avatar,
            })
            fichas.append(ficha)
        categorias = sorted({e.categoria for e in vocab.entradas if e.tipo != "fondo"})
        return {"version": version_vocab, "categorias": categorias, "total": len(fichas), "fichas": fichas}

    @app.post("/v1/retroalimentacion", dependencies=protegido)
    async def recibir_reporte(request: Request) -> dict:
        cuerpo = await request.body()
        if len(cuerpo) > cfg.max_bytes_reporte:
            raise HTTPException(status_code=413, detail="Reporte demasiado grande")
        try:
            import json

            ident = reportes.guardar(json.loads(cuerpo))
        except (ReporteInvalido, ValueError) as e:
            raise HTTPException(status_code=422, detail=f"Reporte rechazado: {e}") from e
        return {"guardado": ident, "estado": "pendiente"}

    @app.get("/v1/retroalimentacion", dependencies=protegido)
    def listar_reportes(estado: Optional[str] = None) -> dict:
        return {"reportes": reportes.listar(estado)}

    @app.get("/v1/retroalimentacion/resumen", dependencies=protegido)
    def resumen_reportes() -> dict:
        return reportes.resumen()

    @app.post("/v1/retroalimentacion/{ident}/revision", dependencies=protegido)
    def revisar_reporte(ident: str, p: PeticionRevision) -> dict:
        try:
            return reportes.revisar(ident, p.estado, p.revisor, p.nota)
        except ReporteInvalido as e:
            raise HTTPException(status_code=422, detail=str(e)) from e
        except KeyError as e:
            raise HTTPException(status_code=404, detail="No existe ese reporte") from e

    @app.post("/v1/traducir/texto", dependencies=protegido)
    def traducir_texto(p: PeticionTexto) -> dict:
        return traductor.texto_a_glosas(p.texto)

    @app.post("/v1/traducir/glosas", dependencies=protegido)
    def traducir_glosas(p: PeticionGlosas) -> dict:
        return traductor.glosas_a_texto(p.glosas)

    async def _leer_audio(audio: UploadFile) -> bytes:
        datos = await audio.read()
        if not datos:
            raise HTTPException(status_code=400, detail="Audio vacío")
        if len(datos) > cfg.max_bytes_audio:
            raise HTTPException(status_code=413, detail="Audio demasiado largo")
        return datos

    def _transcribir(datos: bytes, nombre: str) -> dict:
        try:
            return transcriptor.transcribir(datos, Path(nombre or "audio.wav").suffix or ".wav")
        except VozNoDisponible as e:
            raise HTTPException(status_code=503, detail=str(e)) from e

    @app.post("/v1/voz", dependencies=protegido)
    async def voz(audio: UploadFile = File(...)) -> dict:
        return _transcribir(await _leer_audio(audio), audio.filename)

    @app.post("/v1/voz-a-senas", dependencies=protegido)
    async def voz_a_senas(audio: UploadFile = File(...)) -> dict:
        t = _transcribir(await _leer_audio(audio), audio.filename)
        resultado = traductor.texto_a_glosas(t["texto"])
        resultado["transcripcion"] = t
        return resultado

    @app.post("/v1/recolecciones", dependencies=protegido)
    async def recibir_muestra(request: Request) -> dict:
        cuerpo = await request.body()
        if len(cuerpo) > cfg.max_bytes_muestra:
            raise HTTPException(status_code=413, detail="Muestra demasiado grande")
        try:
            import json

            datos = json.loads(cuerpo)
            ident = almacen.guardar(datos, glosas_validas)
        except (MuestraInvalida, ValueError, KeyError) as e:
            raise HTTPException(status_code=422, detail=f"Muestra rechazada: {e}") from e
        return {"guardada": ident}

    @app.get("/v1/recolecciones/resumen", dependencies=protegido)
    def resumen_recolecciones() -> dict:
        return almacen.resumen()

    @app.get("/v1/avatar/indice", dependencies=protegido)
    def indice_avatar() -> dict:
        return {"senas": avatar.indice()}

    @app.get("/v1/avatar/{glosa}", dependencies=protegido)
    def grabacion_avatar(glosa: str):
        from fastapi.responses import Response

        texto = avatar.leer(glosa)
        if texto is None:
            raise HTTPException(status_code=404, detail=f"No hay grabación de avatar para {glosa}")
        return Response(content=texto, media_type="application/json")

    return app


app = crear_app()
