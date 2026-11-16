# Fuentes de video

`fuentes.csv` es el catálogo de dónde salen los videos de LSRD que alimentan el entrenamiento y el avatar. El canal de videos (`ml/scripts/videos.py`) solo descarga o indexa las fuentes cuyo permiso está resuelto.

| Columna | Contenido |
| --- | --- |
| `id` | Identificador corto, sin espacios. |
| `url` | Dirección de un video, lista o canal; o una carpeta local si `tipo = carpeta`. |
| `tipo` | `video`, `lista`, `canal`, `sitio` o `carpeta`. |
| `titular` | Quién tiene los derechos del material. |
| `permiso` | `pendiente`, `autorizado`, `licencia_abierta` o `propio`. |
| `evidencia_permiso` | Convenio, correo, licencia o consentimiento que lo respalda. Obligatoria salvo en `pendiente`. |
| `uso` | Para qué se autorizó: `entrenamiento`, `avatar`, `referencia` (separados por `\|`). |
| `firmante` | Identificador de la persona que firma, si toda la fuente es de una sola persona. |

## Por qué existe la columna `permiso`

Un video de señas muestra la cara y el cuerpo de una persona identificable, y el Diccionario Oficial tiene derechos reservados del CONADIS. La Ley 172-13 de protección de datos y el propio diseño del proyecto exigen autorización antes de procesar esas imágenes. Por eso una fuente `pendiente` se lista pero no se descarga: se cambia a `autorizado` y se anota la evidencia cuando llegue el permiso.

## Qué se encontró en la búsqueda (noviembre de 2026)

- **Diccionario Oficial en línea** (`diccionariolsrd.cc`): unas 1.466 entradas organizadas por letra y por tema. Es la fuente más valiosa y la que más conviene gestionar con CONADIS.
- **Canal de YouTube de ANSORDO**: videos de la asociación de personas sordas.
- **No existe un conjunto de datos público de LSRD** para reconocimiento automático. Los que hay son de otras lenguas (mexicana, argentina, ASL) y no sirven: son lenguas distintas.

`vocabulario/cobertura_diccionario_oficial.csv` cruza el vocabulario del MVP con el índice del Diccionario Oficial.

## Límite de los videos de diccionario

Un diccionario suele tener una o dos personas por seña. Con eso se puede entrenar un **modelo semilla** (`entrenar.py --semilla`) y animar el avatar, pero no medir si reconoce a personas nuevas. Para un modelo validado siguen haciendo falta las grabaciones de unas 30 personas sordas (`docs/PROTOCOLO_RECOLECCION.md`).
