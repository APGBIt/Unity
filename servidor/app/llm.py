"""Traducción con un modelo de lenguaje (Claude) restringida al vocabulario del MVP.

El modelo propone; el servidor valida. Toda glosa que no esté en el vocabulario
se convierte en deletreo y se reporta como advertencia. Las salidas de este
módulo se marcan siempre como NO validadas: solo las frases revisadas por
intérpretes (memoria de traducción) cuentan como validadas.
"""

from __future__ import annotations

from typing import Any

from .config import Config
from .memoria import Memoria
from .vocabulario import Vocabulario

# Orientaciones gramaticales por defecto. Son hipótesis comunes en lenguas de
# señas emparentadas con la ASL y DEBEN ajustarse con lingüistas sordos de la
# LSRD; los ejemplos validados de la memoria tienen prioridad sobre estas reglas.
REGLAS_LSRD = """\
- La LSRD es una lengua con gramática propia: no traduzca palabra por palabra.
- Use solo glosas de la lista. Para nombres propios, siglas, direcciones o
  términos sin glosa use DACT:palabra (deletreo). Si puede, reformule con glosas
  disponibles en vez de deletrear verbos o palabras comunes.
- Omita artículos, preposiciones, el verbo "ser/estar" y los pronombres clíticos.
- Orden sugerido: tiempo (HOY, MAÑANA…) → tema → comentario. Las palabras
  interrogativas (QUE, DONDE, CUANDO, CUANTO, QUIEN, COMO, CUAL, POR-QUE) van al final.
- Números del 0 al 10 con NUM-0…NUM-10; los demás, cifra por cifra (NUM-2 NUM-5).
- Frases cortas. Si el mensaje no se puede transmitir bien con el vocabulario,
  dígalo en "advertencia" para que el empleado lo escriba o llame a un intérprete.
"""


class TraductorLLM:
    def __init__(self, cfg: Config, vocab: Vocabulario, memoria: Memoria) -> None:
        import anthropic

        self.cfg = cfg
        self.vocab = vocab
        self.memoria = memoria
        self.cliente = anthropic.Anthropic(api_key=cfg.clave_anthropic, timeout=cfg.tiempo_llm_s, max_retries=1)
        lista = "\n".join(
            f"{e.glosa}: {e.espanol}" for e in vocab.entradas if e.tipo in ("seña", "numero")
        )
        ejemplos = "\n".join(f"- {f.espanol} → {' '.join(f.glosas)}" for f in memoria.ejemplos()) or "(todavía no hay)"
        self._sistema_glosas = (
            "Usted traduce del español dominicano a glosas de la Lengua de Señas Dominicana (LSRD) "
            "para un avatar que firma en un mostrador de atención al público.\n\n"
            f"Reglas:\n{REGLAS_LSRD}\n"
            f"Ejemplos validados por intérpretes (tienen prioridad):\n{ejemplos}\n\n"
            f"Vocabulario disponible (glosa: significado):\n{lista}"
        )
        self._sistema_texto = (
            "Usted convierte glosas de la Lengua de Señas Dominicana (LSRD), reconocidas automáticamente "
            "a partir de lo que firma una persona sorda en un mostrador de atención al público, en una "
            "oración breve y natural en español dominicano para el empleado. Hable en primera persona, "
            "como la persona sorda. Las glosas pueden contener errores de reconocimiento: no invente "
            "información que las glosas no sostengan y, si el sentido es dudoso, use confianza 'baja'. "
            "DACT:palabra significa que la persona deletreó esa palabra; NUM-x es un número.\n\n"
            f"Vocabulario (glosa: significado):\n{lista}"
        )

    def _llamar(self, sistema: str, herramienta: dict, mensaje: str) -> dict[str, Any]:
        respuesta = self.cliente.messages.create(
            model=self.cfg.modelo_llm,
            max_tokens=600,
            system=[{"type": "text", "text": sistema, "cache_control": {"type": "ephemeral"}}],
            tools=[herramienta],
            tool_choice={"type": "tool", "name": herramienta["name"]},
            messages=[{"role": "user", "content": mensaje}],
        )
        for bloque in respuesta.content:
            if getattr(bloque, "type", None) == "tool_use":
                return dict(bloque.input)
        raise RuntimeError("El modelo no devolvió la herramienta esperada")

    def texto_a_glosas(self, texto: str) -> dict[str, Any]:
        herramienta = {
            "name": "entregar_glosas",
            "description": "Entrega la traducción a glosas LSRD.",
            "input_schema": {
                "type": "object",
                "properties": {
                    "glosas": {"type": "array", "items": {"type": "string"},
                               "description": "Glosas del vocabulario o DACT:palabra, en orden de firma."},
                    "pregunta": {"type": "boolean", "description": "Verdadero si el mensaje es una pregunta."},
                    "advertencia": {"type": "string", "description": "Vacío, o qué no se pudo transmitir bien."},
                },
                "required": ["glosas", "pregunta", "advertencia"],
            },
        }
        return self._llamar(self._sistema_glosas, herramienta, f"Traduzca: {texto}")

    def glosas_a_texto(self, glosas: list[str]) -> dict[str, Any]:
        herramienta = {
            "name": "entregar_texto",
            "description": "Entrega la oración en español.",
            "input_schema": {
                "type": "object",
                "properties": {
                    "texto": {"type": "string"},
                    "confianza": {"type": "string", "enum": ["alta", "media", "baja"]},
                },
                "required": ["texto", "confianza"],
            },
        }
        return self._llamar(self._sistema_texto, herramienta, "Glosas: " + " ".join(glosas))
