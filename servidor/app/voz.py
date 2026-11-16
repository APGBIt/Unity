"""Voz a texto con Whisper (faster-whisper), opcional.

Instalar con ``pip install faster-whisper``. El primer uso descarga el modelo.
El ``initial_prompt`` sesga el reconocimiento hacia el vocabulario de un
mostrador de atención al público en República Dominicana.
"""

from __future__ import annotations

import os
import tempfile
import threading

PROMPT_DOMINIO = (
    "Atención al público en República Dominicana. Buenos días, ¿en qué le puedo ayudar? "
    "Cédula, acta de nacimiento, pasaporte, turno, cita, formulario, firma, audiencia, expediente, "
    "tribunal, solicitud, recibo, pago, intérprete."
)


class VozNoDisponible(RuntimeError):
    pass


class Transcriptor:
    def __init__(self, modelo: str = "small") -> None:
        self.nombre_modelo = modelo
        self._modelo = None
        self._candado = threading.Lock()

    @staticmethod
    def disponible() -> bool:
        try:
            import faster_whisper  # noqa: F401
        except ImportError:
            return False
        return True

    def _cargar(self):
        if self._modelo is None:
            with self._candado:
                if self._modelo is None:
                    try:
                        from faster_whisper import WhisperModel
                    except ImportError as e:
                        raise VozNoDisponible(
                            "La voz a texto no está instalada en el servidor (pip install faster-whisper)."
                        ) from e
                    self._modelo = WhisperModel(self.nombre_modelo, device="cpu", compute_type="int8")
        return self._modelo

    def transcribir(self, audio: bytes, sufijo: str = ".wav") -> dict:
        modelo = self._cargar()
        with tempfile.NamedTemporaryFile(suffix=sufijo, delete=False) as fh:
            fh.write(audio)
            ruta = fh.name
        try:
            segmentos, info = modelo.transcribe(
                ruta, language="es", initial_prompt=PROMPT_DOMINIO, vad_filter=True, beam_size=1,
            )
            texto = " ".join(s.text.strip() for s in segmentos).strip()
            return {"texto": texto, "idioma": info.language, "duracion_s": round(float(info.duration), 2)}
        finally:
            os.unlink(ruta)
