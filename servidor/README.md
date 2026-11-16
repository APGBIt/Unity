# Servidor LSRD

API que da a la app: traducción entre glosas LSRD y español, voz a texto, recepción de grabaciones del modo recolección y la biblioteca de grabaciones del avatar.

```bash
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
python -m pytest -q
```

Documentación interactiva: `http://<servidor>:8000/docs`.

## Variables de entorno

| Variable | Para qué | Por defecto |
| --- | --- | --- |
| `LSRD_CLAVE` | Clave que la app envía en `X-Clave-LSRD`. Sin clave, cualquiera en la red puede usar la API (solo para desarrollo). | sin clave |
| `ANTHROPIC_API_KEY` | Activa la traducción con Claude. Sin ella se usa memoria validada + traducción literal. | — |
| `LSRD_MODELO_LLM` | Modelo de Claude. `claude-haiku-4-5` responde más rápido; `claude-sonnet-5-5` traduce mejor. | `claude-sonnet-5-5` |
| `LSRD_WHISPER` | Tamaño del modelo Whisper para voz a texto (`small`, `medium`…). Requiere `pip install faster-whisper`. | `small` |
| `LSRD_DATOS` | Carpeta de grabaciones recibidas y del avatar. | `servidor/datos` |
| `LSRD_VOCABULARIO`, `LSRD_FRASES` | Rutas del vocabulario y de la memoria de frases. | `../vocabulario/…` |

## Cómo traduce

1. **Memoria validada.** Si la frase (o la secuencia de glosas) está en `vocabulario/frases_mvp.csv` con `estado = validada`, se usa la traducción del intérprete. Es la única salida marcada como *validada*.
2. **Modelo de lenguaje (opcional).** Claude traduce con el vocabulario cerrado del MVP y los ejemplos validados. El servidor verifica cada glosa: lo que no está en el vocabulario se deletrea y se avisa.
3. **Literal.** Sin modelo o si falla: palabra por palabra con los sinónimos del vocabulario; se marca como literal.

Las reglas gramaticales que recibe el modelo están en `app/llm.py` (`REGLAS_LSRD`). Son hipótesis a revisar con lingüistas sordos de la LSRD.

## Rutas

| Método | Ruta | Uso |
| --- | --- | --- |
| GET | `/salud` | Estado, versión del vocabulario, servicios activos |
| GET | `/v1/vocabulario`, `/v1/frases` | Vocabulario y frases (rápidas con `solo_rapidas=true`) |
| POST | `/v1/traducir/texto` | Español → glosas y plan del avatar |
| POST | `/v1/traducir/glosas` | Glosas → español |
| POST | `/v1/voz`, `/v1/voz-a-senas` | Audio WAV → texto (y → glosas) |
| POST | `/v1/recolecciones` | Recibe una grabación (exige consentimiento) |
| GET | `/v1/recolecciones/resumen` | Avance de la recolección por glosa y firmante |
| GET | `/v1/avatar/indice`, `/v1/avatar/{glosa}` | Grabaciones de referencia del avatar |
| GET | `/v1/emergencias` | Frases de acceso rápido para emergencias |
| GET | `/v1/diccionario` | Fichas del diccionario (`buscar`, `categoria`), con referencia oficial y si hay animación |
| POST | `/v1/retroalimentacion` | Recibe un reporte (solo texto). Queda `pendiente` |
| GET | `/v1/retroalimentacion`, `/v1/retroalimentacion/resumen` | Reportes por estado y señas más reportadas |
| POST | `/v1/retroalimentacion/{id}/revision` | Quien valida lo marca `aceptado` o `descartado`. No cambia el vocabulario |

## Datos

- `datos/recolecciones/<firmante>/<GLOSA>__<id>.json`: tomas recibidas (solo puntos clave). Es la entrada de `ml/scripts/entrenar.py --datos servidor/datos/recolecciones`.
- `datos/avatar/<GLOSA>.json`: referencias del avatar. Para publicar una toma revisada por una persona sorda:
  ```bash
  python publicar_avatar.py datos/recolecciones/P017/GRACIAS__abc123.json
  ```
  Solo acepta tomas con consentimiento explícito para el avatar.

Para borrar los datos de un participante, elimine su carpeta en `datos/recolecciones/` y sus archivos en `datos/avatar/`, y vuelva a entrenar.

## Producción

Use HTTPS (por ejemplo, detrás de un proxy inverso), defina `LSRD_CLAVE`, limite el acceso a la red institucional y respalde `datos/` cifrado.
