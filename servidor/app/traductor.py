"""Traducción en las dos direcciones: memoria validada → modelo de lenguaje → literal.

* **Español → glosas** (lo que dice el empleado; lo firma el avatar).
* **Glosas → español** (lo que firma la persona sorda; lo lee/escucha el empleado).

Cada resultado indica su ``fuente`` y si está ``validada`` por intérpretes, para
que la app muestre un aviso cuando la traducción es automática.
"""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass, field
from typing import Any, Optional

from .memoria import Memoria
from .vocabulario import FUNCIONALES, PREGUNTAS, Vocabulario, normalizar

log = logging.getLogger("lsrd.traductor")
_NUM = re.compile(r"^NUM-(\d+)$")


@dataclass
class ItemPlan:
    tipo: str  # "seña" | "deletreo"
    glosa: str = ""
    espanol: str = ""
    palabra: str = ""
    letras: list[str] = field(default_factory=list)

    def a_dict(self) -> dict:
        if self.tipo == "seña":
            return {"tipo": "seña", "glosa": self.glosa, "espanol": self.espanol}
        return {"tipo": "deletreo", "palabra": self.palabra, "letras": self.letras}


class Traductor:
    def __init__(self, vocab: Vocabulario, memoria: Memoria, llm: Optional[Any] = None) -> None:
        self.vocab = vocab
        self.memoria = memoria
        self.llm = llm

    # ------------------------------------------------------------------
    # Español → glosas
    # ------------------------------------------------------------------
    def texto_a_glosas(self, texto: str) -> dict:
        texto = texto.strip()
        if not texto:
            return self._respuesta_glosas([], texto, "vacía", False, False, [])
        es_pregunta = "?" in texto or "¿" in texto

        frase, similitud = self.memoria.buscar_texto(texto)
        if frase is not None:
            return self._respuesta_glosas(list(frase.glosas), texto, "memoria", True, es_pregunta, [],
                                          similitud=similitud)

        advertencias: list[str] = []
        if self.llm is not None:
            try:
                r = self.llm.texto_a_glosas(texto)
                glosas = [str(g).strip() for g in r.get("glosas", []) if str(g).strip()]
                if r.get("advertencia"):
                    advertencias.append(str(r["advertencia"]))
                if glosas:
                    return self._respuesta_glosas(glosas, texto, "llm", False,
                                                  bool(r.get("pregunta", es_pregunta)), advertencias)
            except Exception as e:  # noqa: BLE001 — cualquier fallo cae a la traducción literal
                log.warning("Falló la traducción con LLM: %s", e)
                advertencias.append("El servicio de traducción automática no respondió; se usó traducción literal.")

        glosas = self.literal_texto_a_glosas(texto)
        advertencias.append("Traducción literal palabra por palabra: no sigue la gramática de la LSRD.")
        return self._respuesta_glosas(glosas, texto, "literal", False, es_pregunta, advertencias)

    def literal_texto_a_glosas(self, texto: str) -> list[str]:
        tokens = normalizar(texto).split()
        salida: list[str] = []
        i = 0
        while i < len(tokens):
            encontrada = None
            for n in range(min(self.vocab.max_palabras, len(tokens) - i), 0, -1):
                g = self.vocab.buscar(" ".join(tokens[i:i + n]))
                if g is not None:
                    encontrada = (g, n)
                    break
            if encontrada:
                salida.append(encontrada[0])
                i += encontrada[1]
                continue
            tok = tokens[i]
            i += 1
            if tok.isdigit():
                salida.extend(self._numero(tok))
            elif tok in FUNCIONALES:
                continue
            else:
                salida.append(f"DACT:{tok}")
        return salida

    def _numero(self, cifras: str) -> list[str]:
        n = int(cifras)
        if n <= 10:
            return [f"NUM-{n}"]
        return [f"NUM-{c}" for c in cifras]

    def _plan(self, glosas: list[str], advertencias: list[str]) -> list[ItemPlan]:
        plan: list[ItemPlan] = []
        for g in glosas:
            if g.upper().startswith("DACT:"):
                palabra = g[5:].strip().lower()
                letras = self.vocab.deletreo(palabra)
                if letras:
                    plan.append(ItemPlan("deletreo", palabra=palabra, letras=letras))
                continue
            gu = g.upper()
            m = _NUM.match(gu)
            if m and not self.vocab.existe(gu):
                for c in m.group(1):
                    plan.append(ItemPlan("seña", glosa=f"NUM-{c}", espanol=c))
                continue
            if self.vocab.existe(gu):
                e = self.vocab.por_glosa[gu]
                if e.tipo == "letra":
                    plan.append(ItemPlan("deletreo", palabra=e.espanol, letras=[gu]))
                else:
                    plan.append(ItemPlan("seña", glosa=gu, espanol=self.vocab.espanol_de(gu)))
                continue
            # Glosa fuera del vocabulario: intentar reconocerla como palabra, si no, deletrear
            alterna = self.vocab.buscar(normalizar(g.replace("-", " ")))
            if alterna:
                plan.append(ItemPlan("seña", glosa=alterna, espanol=self.vocab.espanol_de(alterna)))
            else:
                palabra = g.replace("-", " ").lower()
                advertencias.append(f"'{g}' no está en el vocabulario; se deletrea.")
                letras = self.vocab.deletreo(palabra)
                if letras:
                    plan.append(ItemPlan("deletreo", palabra=palabra, letras=letras))
        return plan

    def _respuesta_glosas(self, glosas, texto, fuente, validada, pregunta, advertencias, similitud=None) -> dict:
        advertencias = list(advertencias)
        plan = self._plan(glosas, advertencias)
        r = {
            "texto": texto,
            "glosas": [p.glosa if p.tipo == "seña" else f"DACT:{p.palabra}" for p in plan],
            "plan": [p.a_dict() for p in plan],
            "pregunta": pregunta,
            "fuente": fuente,
            "validada": validada,
            "advertencias": advertencias,
        }
        if similitud is not None:
            r["similitud_memoria"] = round(similitud, 3)
        return r

    # ------------------------------------------------------------------
    # Glosas → español
    # ------------------------------------------------------------------
    def glosas_a_texto(self, glosas: list[str]) -> dict:
        glosas = [g.strip().upper() if not g.upper().startswith("DACT:") else "DACT:" + g[5:].strip()
                  for g in glosas if g and g.strip() and g.strip() != "_FONDO"]
        if not glosas:
            return {"texto": "", "fuente": "vacía", "validada": False, "confianza": "baja", "advertencias": []}

        frase = self.memoria.buscar_glosas(glosas)
        if frase is not None:
            return {"texto": frase.espanol, "fuente": "memoria", "validada": True, "confianza": "alta",
                    "advertencias": []}
        advertencias = []
        if self.llm is not None:
            try:
                r = self.llm.glosas_a_texto(glosas)
                texto = str(r.get("texto", "")).strip()
                if texto:
                    return {"texto": texto, "fuente": "llm", "validada": False,
                            "confianza": r.get("confianza", "media"), "advertencias": []}
            except Exception as e:  # noqa: BLE001
                log.warning("Falló la traducción con LLM: %s", e)
                advertencias.append("El servicio de traducción automática no respondió; se usó traducción literal.")
        return {"texto": self.literal_glosas_a_texto(glosas), "fuente": "literal", "validada": False,
                "confianza": "baja", "advertencias": advertencias + ["Traducción literal de las glosas."]}

    def literal_glosas_a_texto(self, glosas: list[str]) -> str:
        palabras = []
        for g in glosas:
            m = _NUM.match(g)
            if m:
                palabras.append(m.group(1))
            else:
                palabras.append(self.vocab.espanol_de(g))
        texto = " ".join(palabras).strip()
        if not texto:
            return ""
        texto = texto[0].upper() + texto[1:]
        return ("¿" + texto + "?") if any(g in PREGUNTAS for g in glosas) else texto + "."
