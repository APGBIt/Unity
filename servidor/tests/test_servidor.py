import base64
import csv
import io
import struct
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.config import Config  # noqa: E402
from app.main import crear_app  # noqa: E402
from app.memoria import Memoria  # noqa: E402
from app.traductor import Traductor  # noqa: E402
from app.vocabulario import Vocabulario, normalizar, sin_tildes  # noqa: E402
from app.voz import VozNoDisponible  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
VOCAB = RAIZ / "vocabulario" / "vocabulario_mvp.csv"
FRASES = RAIZ / "vocabulario" / "frases_mvp.csv"


@pytest.fixture(scope="module")
def vocab():
    return Vocabulario(VOCAB)


@pytest.fixture()
def frases_validadas(tmp_path):
    """Copia de frases_mvp.csv con dos frases marcadas como validadas (solo para pruebas)."""
    with open(FRASES, encoding="utf-8", newline="") as fh:
        filas = list(csv.DictReader(fh))
    for f in filas:
        if f["id"] == "F033":
            f["glosas"], f["estado"] = "INTERPRETE NECESITAR", "validada"
        if f["id"] == "F010":
            f["glosas"], f["estado"] = "CITA TENER TU", "validada"
    ruta = tmp_path / "frases.csv"
    with open(ruta, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=list(filas[0]))
        w.writeheader()
        w.writerows(filas)
    return ruta


class LLMFalso:
    def __init__(self, glosas=None, falla=False):
        self.glosas = glosas or ["HOY", "CITA", "GLOSA-INEXISTENTE", "DACT:Pérez", "NUM-25"]
        self.falla = falla
        self.llamadas = 0

    def texto_a_glosas(self, texto):
        self.llamadas += 1
        if self.falla:
            raise TimeoutError("sin red")
        return {"glosas": self.glosas, "pregunta": False, "advertencia": ""}

    def glosas_a_texto(self, glosas):
        self.llamadas += 1
        if self.falla:
            raise TimeoutError("sin red")
        return {"texto": "Necesito una cita para hoy.", "confianza": "media"}


class TranscriptorFalso:
    def __init__(self, disponible=True):
        self.ok = disponible

    def transcribir(self, audio, sufijo=".wav"):
        if not self.ok:
            raise VozNoDisponible("no instalado")
        return {"texto": "¿Tiene cita?", "idioma": "es", "duracion_s": 1.2}


# ---------------------------------------------------------------- vocabulario

def test_normalizar():
    assert normalizar("¿Me permite su CÉDULA, por favor?") == "me permite su cédula por favor"
    assert sin_tildes("cédula año") == "cedula año"


def test_busqueda_con_y_sin_tildes(vocab):
    assert vocab.buscar("cédula") == "CEDULA"
    assert vocab.buscar("cedula") == "CEDULA"
    assert vocab.buscar("qué") == "QUE"
    assert vocab.buscar("que") is None  # "que" sin tilde es funcional, no pregunta
    assert vocab.buscar("buenos días") == "BUENOS-DIAS"


def test_deletreo(vocab):
    assert vocab.deletreo("Peña") == ["LETRA-P", "LETRA-E", "LETRA-Ñ", "LETRA-A"]


# ---------------------------------------------------------------- traducción

def test_literal_usa_frases_de_varias_palabras(vocab):
    t = Traductor(vocab, Memoria(FRASES))
    assert t.literal_texto_a_glosas("Buenos días, ¿en qué le puedo ayudar?") == ["BUENOS-DIAS", "QUE", "PODER", "AYUDAR"]
    assert t.literal_texto_a_glosas("No entiendo") == ["NO-ENTENDER"]
    assert t.literal_texto_a_glosas("Vuelva en 15 días") == ["VOLVER", "NUM-1", "NUM-5", "DIA"]


def test_sin_llm_marca_traduccion_literal(vocab):
    r = Traductor(vocab, Memoria(FRASES)).texto_a_glosas("Firme aquí, por favor.")
    assert r["glosas"] == ["FIRMAR", "AQUI", "POR-FAVOR"]
    assert r["fuente"] == "literal" and r["validada"] is False and r["advertencias"]


def test_palabra_desconocida_se_deletrea(vocab):
    r = Traductor(vocab, Memoria(FRASES)).texto_a_glosas("Pregunte por Yudelka")
    deletreos = [p for p in r["plan"] if p["tipo"] == "deletreo"]
    assert any(p["palabra"] == "yudelka" and p["letras"][0] == "LETRA-Y" for p in deletreos)


def test_memoria_validada_tiene_prioridad(vocab, frases_validadas):
    llm = LLMFalso()
    t = Traductor(vocab, Memoria(frases_validadas), llm)
    r = t.texto_a_glosas("necesito un interprete")  # sin tildes ni puntuación: coincide por similitud
    assert r["fuente"] == "memoria" and r["validada"] is True
    assert r["glosas"] == ["INTERPRETE", "NECESITAR"]
    assert llm.llamadas == 0
    assert t.glosas_a_texto(["CITA", "TENER", "TU"])["texto"] == "¿Tiene cita?"


def test_salida_del_llm_se_valida_contra_el_vocabulario(vocab):
    r = Traductor(vocab, Memoria(FRASES), LLMFalso()).texto_a_glosas("Su cita con el señor Pérez es hoy")
    assert r["fuente"] == "llm" and r["validada"] is False
    tipos = [(p["tipo"], p.get("glosa") or p.get("palabra")) for p in r["plan"]]
    assert tipos[:2] == [("seña", "HOY"), ("seña", "CITA")]
    assert ("deletreo", "glosa inexistente") in tipos
    assert ("deletreo", "pérez") in tipos
    assert ("seña", "NUM-2") in tipos and ("seña", "NUM-5") in tipos
    assert any("GLOSA-INEXISTENTE" in a for a in r["advertencias"])


def test_si_el_llm_falla_se_usa_literal(vocab):
    r = Traductor(vocab, Memoria(FRASES), LLMFalso(falla=True)).texto_a_glosas("Tome asiento")
    assert r["fuente"] == "literal" and r["glosas"] == ["SENTAR"]


def test_glosas_a_texto_literal(vocab):
    t = Traductor(vocab, Memoria(FRASES))
    r = t.glosas_a_texto(["_FONDO", "BAÑO", "DONDE"])
    assert r["texto"] == "¿Baño dónde?" and r["fuente"] == "literal"
    assert t.glosas_a_texto(["YO", "NECESITAR", "CITA"])["texto"] == "Yo necesitar cita."


# ---------------------------------------------------------------- API

def _app(tmp_path, clave=None, llm=None, voz=True, frases=FRASES):
    cfg = Config(vocabulario=VOCAB, frases=frases, datos=tmp_path / "datos", clave=clave, clave_anthropic=None)
    return TestClient(crear_app(cfg, llm=llm, transcriptor=TranscriptorFalso(voz)))


def _wav() -> bytes:
    buf = io.BytesIO()
    n = 1600
    buf.write(b"RIFF" + struct.pack("<I", 36 + n * 2) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, 16000, 32000, 2, 16))
    buf.write(b"data" + struct.pack("<I", n * 2) + b"\x00\x00" * n)
    return buf.getvalue()


def _muestra(glosa="GRACIAS", consentimiento=True, cuadros=3):
    datos = b"\x00" * (cuadros * 78 * 3 * 4)
    return {
        "formato": "LSRD-LM v1", "glosa": glosa, "firmante": "P001", "sesion": "S1", "id": "abc",
        "forma": [cuadros, 78, 3], "tiempos_ms": [0, 33, 66][:cuadros],
        "cuadros_b64": base64.b64encode(datos).decode(),
        "consentimiento": {"puntos_clave": consentimiento, "version": "1.0"},
    }


def test_api_salud_y_vocabulario(tmp_path):
    c = _app(tmp_path)
    s = c.get("/salud").json()
    assert s["estado"] == "ok" and s["traduccion_llm"] is False and s["vocabulario"] == 194
    v = c.get("/v1/vocabulario").json()
    assert len(v["entradas"]) == 194 and v["version"] == s["version_vocabulario"]
    rapidas = c.get("/v1/frases", params={"direccion": "sordo_a_oyente", "solo_rapidas": True}).json()["frases"]
    assert rapidas and all(f["direccion"] == "sordo_a_oyente" and f["rapida"] for f in rapidas)


def test_api_clave(tmp_path):
    c = _app(tmp_path, clave="secreta")
    assert c.get("/v1/vocabulario").status_code == 401
    assert c.get("/v1/vocabulario", headers={"X-Clave-LSRD": "otra"}).status_code == 401
    assert c.get("/v1/vocabulario", headers={"X-Clave-LSRD": "secreta"}).status_code == 200
    assert c.get("/salud").status_code == 200


def test_api_traducciones(tmp_path):
    c = _app(tmp_path, llm=LLMFalso())
    r = c.post("/v1/traducir/texto", json={"texto": "¿Tiene cita?"}).json()
    assert r["fuente"] == "llm" and r["plan"]
    g = c.post("/v1/traducir/glosas", json={"glosas": ["YO", "NECESITAR", "CITA"]}).json()
    assert g["texto"] == "Necesito una cita para hoy."
    assert c.post("/v1/traducir/texto", json={"texto": "x" * 600}).status_code == 422


def test_api_voz(tmp_path):
    c = _app(tmp_path)
    r = c.post("/v1/voz-a-senas", files={"audio": ("a.wav", _wav(), "audio/wav")}).json()
    assert r["transcripcion"]["texto"] == "¿Tiene cita?" and r["pregunta"] is True
    sin_voz = _app(tmp_path, voz=False)
    assert sin_voz.post("/v1/voz", files={"audio": ("a.wav", _wav(), "audio/wav")}).status_code == 503
    assert c.post("/v1/voz", files={"audio": ("a.wav", b"", "audio/wav")}).status_code == 400


def test_api_recolecciones(tmp_path):
    c = _app(tmp_path)
    assert c.post("/v1/recolecciones", json=_muestra()).json() == {"guardada": "abc"}
    assert c.post("/v1/recolecciones", json=_muestra(consentimiento=False)).status_code == 422
    assert c.post("/v1/recolecciones", json=_muestra(glosa="NO-EXISTE")).status_code == 422
    malo = _muestra()
    malo["cuadros_b64"] = base64.b64encode(b"\x00" * 10).decode()
    assert c.post("/v1/recolecciones", json=malo).status_code == 422
    traversal = _muestra()
    traversal["firmante"] = "../../etc"
    traversal["id"] = "../x"
    assert c.post("/v1/recolecciones", json=traversal).status_code == 200
    assert not (tmp_path / "etc").exists()
    resumen = c.get("/v1/recolecciones/resumen").json()
    assert resumen["total"] == 2 and resumen["por_glosa"]["GRACIAS"]["muestras"] == 2


def test_api_avatar(tmp_path):
    from app.recolecciones import AvatarAlmacen, MuestraInvalida

    c = _app(tmp_path)
    assert c.get("/v1/avatar/indice").json() == {"senas": []}
    almacen = AvatarAlmacen(tmp_path / "datos")
    sin_permiso = _muestra()
    with pytest.raises(MuestraInvalida):
        almacen.publicar(sin_permiso, {"GRACIAS"})
    con_permiso = _muestra()
    con_permiso["consentimiento"]["avatar"] = True
    almacen.publicar(con_permiso, {"GRACIAS"})
    indice = c.get("/v1/avatar/indice").json()["senas"]
    assert [s["glosa"] for s in indice] == ["GRACIAS"] and len(indice[0]["version"]) == 12
    r = c.get("/v1/avatar/GRACIAS")
    assert r.status_code == 200 and r.json()["glosa"] == "GRACIAS"
    assert c.get("/v1/avatar/NADA").status_code == 404
    assert c.get("/v1/avatar/..%2F..%2Fetc").status_code == 404


# ---------------------------------------------------------------- cliente LLM (sin red)

def test_cliente_llm_arma_la_peticion(monkeypatch, vocab, frases_validadas):
    import anthropic
    from types import SimpleNamespace

    from app.llm import TraductorLLM

    capturado = {}

    class Mensajes:
        def create(self, **kw):
            capturado.update(kw)
            nombre = kw["tool_choice"]["name"]
            entrada = ({"glosas": ["CITA", "TENER", "TU"], "pregunta": True, "advertencia": ""}
                       if nombre == "entregar_glosas" else {"texto": "¿Tiene cita?", "confianza": "alta"})
            return SimpleNamespace(content=[SimpleNamespace(type="text", text="..."),
                                            SimpleNamespace(type="tool_use", input=entrada)])

    class ClienteFalso:
        def __init__(self, **kw):
            self.messages = Mensajes()

    monkeypatch.setattr(anthropic, "Anthropic", ClienteFalso)
    cfg = Config(vocabulario=VOCAB, frases=frases_validadas, clave_anthropic="sk-prueba")
    llm = TraductorLLM(cfg, vocab, Memoria(frases_validadas))
    r = llm.texto_a_glosas("¿Tiene cita?")
    assert r["glosas"] == ["CITA", "TENER", "TU"]
    assert capturado["tool_choice"] == {"type": "tool", "name": "entregar_glosas"}
    assert capturado["model"] == cfg.modelo_llm
    assert capturado["system"][0]["cache_control"] == {"type": "ephemeral"}
    assert "INTERPRETE NECESITAR" in capturado["system"][0]["text"]  # ejemplos validados en el prompt
    assert "CEDULA: cédula" in capturado["system"][0]["text"]
    assert llm.glosas_a_texto(["CITA", "TENER", "TU"])["texto"] == "¿Tiene cita?"


# ---------------------------------------------------------------- emergencias, diccionario y retroalimentación

def test_emergencias_en_las_dos_direcciones(tmp_path):
    c = _app(tmp_path)
    todas = c.get("/v1/emergencias").json()["frases"]
    assert len(todas) >= 12 and all(f["rapida"] for f in todas)
    sordo = c.get("/v1/emergencias", params={"direccion": "sordo_a_oyente"}).json()["frases"]
    assert any("9-1-1" in f["espanol"] for f in sordo)
    assert {f["direccion"] for f in todas} == {"sordo_a_oyente", "oyente_a_sordo"}
    # Ninguna se presenta como validada mientras un intérprete no escriba las glosas.
    assert not any(f["validada"] for f in todas)


def test_diccionario_busca_sin_tildes_y_trae_la_referencia_oficial(tmp_path):
    c = _app(tmp_path)
    d = c.get("/v1/diccionario").json()
    assert d["total"] == 193 and "tramites" in d["categorias"]
    assert not any(f["tipo"] == "fondo" for f in d["fichas"])
    r = c.get("/v1/diccionario", params={"buscar": "CEDULA"}).json()
    assert [f["glosa"] for f in r["fichas"]] == ["CEDULA"]
    assert r["fichas"][0]["referencia_oficial"] is None and r["fichas"][0]["tiene_animacion"] is False
    r = c.get("/v1/diccionario", params={"buscar": "gracias"}).json()
    ficha = next(f for f in r["fichas"] if f["glosa"] == "GRACIAS")
    assert ficha["referencia_oficial"]["url"] == "https://diccionariolsrd.cc/diccionario/gracias/"
    solo = c.get("/v1/diccionario", params={"categoria": "saludos"}).json()
    assert solo["total"] >= 4 and all(f["categoria"] == "saludos" for f in solo["fichas"])


def test_retroalimentacion_queda_pendiente_y_no_toca_el_vocabulario(tmp_path):
    c = _app(tmp_path)
    antes = c.get("/v1/vocabulario").json()
    reporte = {"id": "rep-000001", "tipo": "traduccion_incorrecta", "direccion": "senas_a_texto",
               "texto_mostrado": "Necesito una cita.", "glosas": ["CITA", "NECESITAR"],
               "correccion": "Quise decir: ¿tiene cita?", "fuente": "literal"}
    r = c.post("/v1/retroalimentacion", json=reporte)
    assert r.status_code == 200 and r.json() == {"guardado": "rep-000001", "estado": "pendiente"}
    assert c.post("/v1/retroalimentacion", json={"tipo": "inventado", "texto_mostrado": "x"}).status_code == 422
    assert c.post("/v1/retroalimentacion", json={"tipo": "otro"}).status_code == 422
    assert c.post("/v1/retroalimentacion", json={"tipo": "otro", "correccion": "x" * 501}).status_code == 422
    assert c.post("/v1/retroalimentacion", json={"tipo": "otro", "id": "../x", "correccion": "x"}).status_code == 422

    lista = c.get("/v1/retroalimentacion", params={"estado": "pendiente"}).json()["reportes"]
    assert len(lista) == 1 and lista[0]["correccion"].startswith("Quise decir")
    resumen = c.get("/v1/retroalimentacion/resumen").json()
    assert resumen["por_estado"] == {"pendiente": 1}
    assert resumen["glosas_mas_reportadas"][0] == {"glosa": "CITA", "reportes": 1}

    assert c.post("/v1/retroalimentacion/rep-000001/revision", json={"estado": "listo", "revisor": "Ana"}).status_code == 422
    assert c.post("/v1/retroalimentacion/no-existe/revision", json={"estado": "aceptado", "revisor": "Ana"}).status_code == 404
    rev = c.post("/v1/retroalimentacion/rep-000001/revision",
                 json={"estado": "aceptado", "revisor": "Ana (intérprete)", "nota": "Falta la seña de pregunta."}).json()
    assert rev["estado"] == "aceptado" and rev["revision"]["revisor"].startswith("Ana")
    assert c.get("/v1/retroalimentacion", params={"estado": "pendiente"}).json()["reportes"] == []
    # Revisar un reporte no cambia nada más: el vocabulario sigue igual.
    assert c.get("/v1/vocabulario").json() == antes


def test_retroalimentacion_exige_clave(tmp_path):
    c = _app(tmp_path, clave="secreta")
    assert c.post("/v1/retroalimentacion", json={"tipo": "otro", "correccion": "x"}).status_code == 401
    assert c.get("/v1/retroalimentacion").status_code == 401
