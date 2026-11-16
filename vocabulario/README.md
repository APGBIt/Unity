# Vocabulario del MVP — atención al público

Estos dos archivos son la fuente de verdad del vocabulario. El modelo de reconocimiento, el servidor de traducción, el avatar y el modo de recolección de la app los leen.

**Nada de lo que hay aquí está validado todavía.** La lista es una propuesta de trabajo para revisar con ANSORDO, CONADIS e intérpretes certificados. La columna `estado` cambia a `validada` solo cuando una persona sorda nativa de LSRD lo aprueba.

## `vocabulario_mvp.csv`

| Columna | Qué contiene |
| --- | --- |
| `glosa` | Identificador de la seña en mayúsculas, sin tildes, con guiones (`BUENOS-DIAS`). La Ñ se conserva (`AÑO`). Es la etiqueta que predice el modelo. |
| `espanol` | Forma principal en español. |
| `sinonimos` | Palabras o frases en español que se traducen a esta glosa, separadas por `\|`. El servidor las usa para la traducción sin conexión. |
| `categoria` | Agrupación temática (saludos, trámites, justicia, tiempo…). |
| `prioridad` | `1` = núcleo del MVP, `2` = segunda ola. |
| `tipo` | `seña`, `numero`, `letra` o `fondo`. |
| `modelo` | Qué modelo la reconoce: `senas` (secuencias) o `dactilologia` (letras, modo deletreo). |
| `estado` | `propuesta` → `validada`. |
| `notas` | Dudas lingüísticas a resolver con la comunidad. |

La clase `_FONDO` no es una seña: agrupa movimientos naturales que no son señas. Sin ella el modelo siempre "adivina" alguna seña aunque la persona solo se esté acomodando.

Totales actuales: 155 señas, 11 números, 27 letras y la clase de fondo (194 filas).

## `frases_mvp.csv`

Frases frecuentes de un mostrador de atención al público, en las dos direcciones.

- `glosas` queda **vacía** hasta que un intérprete certificado escriba la traducción LSRD (secuencia de glosas separadas por espacios, p. ej. `HOY CITA TU`). Solo las frases con `estado = validada` se usan como memoria de traducción.
- `rapida = 1` marca las frases que aparecen como botones de acceso rápido en la app.
- `direccion` es `oyente_a_sordo` (lo dice el empleado; el avatar lo firma) o `sordo_a_oyente` (lo selecciona la persona sorda; la app lo dice en voz alta).

## Cómo se valida

1. Sesión con 2–3 personas sordas nativas y un intérprete por cada categoría.
2. Para cada glosa: confirmar que existe como seña en LSRD, anotar variantes regionales y decidir cuál se graba primero.
3. Para cada frase: el intérprete escribe la secuencia de glosas; una persona sorda la revisa.
4. Cambiar `estado` a `validada` y registrar quién validó en `notas`.
5. Las señas que no existan se quitan o se marcan para deletreo.

## `cobertura_diccionario_oficial.csv`

Cruce del vocabulario del MVP con el índice público del [Diccionario Oficial](https://diccionariolsrd.cc/diccionario/) (unas 1.466 entradas). De las 155 señas propuestas se localizaron 60. La consulta fue automática y parcial: `no_localizada` significa que no se encontró el título en el índice, no que la seña no exista. Sirve para preparar la validación:

- Las 60 localizadas tienen referencia oficial; son las primeras candidatas a grabar.
- Trámites, justicia, días de la semana y palabras para preguntar casi no aparecen (la sección «Palabras para preguntar» del diccionario figura sin artículos). Esas señas hay que acordarlas directamente con ANSORDO.

El archivo solo contiene títulos y direcciones. No se copió ningún video, imagen ni definición del diccionario, que tiene derechos reservados del CONADIS.
