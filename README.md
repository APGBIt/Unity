# Señas RD · Traductor de Lengua de Señas Dominicana

**App para el teléfono:** https://apgbit.github.io/Unity/ — se abre en el navegador y se instala desde ahí (Android: menú ⋮ → «Instalar app»; iPhone: Compartir → «Agregar a inicio»).

Traduce en las dos direcciones:

- **Persona sorda → oyente:** la cámara lee las señas, la app arma la frase y la dice en voz alta.
- **Oyente → persona sorda:** quien atiende habla o escribe, y la app lo muestra en grande y con las señas enseñadas.

Incluye un **acceso de emergencia** que funciona sin conexión, un **diccionario** y un modo para **enseñarle señas**.

> **Cómo reconoce.** No existe un conjunto público de grabaciones de LSRD, así que la app aprende por ejemplos: cada persona le enseña sus señas (tres veces cada una) y desde ese momento las reconoce. Reconoce señas sueltas con una pausa entre ellas, traduce palabra por palabra y no usa todavía la expresión de la cara. Detalles y mediciones en [web/README.md](web/README.md).

El repositorio tiene dos apps. La **app web** (`web/`, publicada en `docs/`) es la que se usa hoy en el teléfono. La **app Unity** (`unity/`) es la base para una versión nativa con un modelo entrenado; sus modelos incluidos son de demostración y no reconocen LSRD.

Los requisitos del producto y su estado están en [documentacion/REQUISITOS.md](documentacion/REQUISITOS.md).

## Estructura

| Carpeta | Qué hay | Guía |
| --- | --- | --- |
| `web/` | App web instalable: reconocimiento por ejemplos en el teléfono, conversación, emergencia, diccionario. | [web/README.md](web/README.md) |
| `docs/` | La app web ya construida, tal como la publica GitHub Pages. No se edita a mano. | — |
| `vocabulario/` | Vocabulario del MVP (155 señas, 11 números, 27 letras), 50 frases de mostrador, 16 de emergencia y el cruce con el Diccionario Oficial. Todo marcado *pendiente de validación* por la comunidad sorda. | [vocabulario/README.md](vocabulario/README.md) |
| `fuentes/` | Catálogo de fuentes de video de LSRD con el estado de su permiso de uso. | [fuentes/README.md](fuentes/README.md) |
| `unity/LSRD-AR/` | La app: cámara, detector MediaPipe holístico, reconocimiento con Unity Inference Engine, interfaz, avatar, modo recolección. | [unity/LSRD-AR/README.md](unity/LSRD-AR/README.md) |
| `servidor/` | API FastAPI: traducción glosas ↔ español (memoria validada → Claude opcional → literal), voz a texto (Whisper opcional), recepción de grabaciones, biblioteca del avatar, diccionario y reportes de retroalimentación. | [servidor/README.md](servidor/README.md) |
| `ml/` | Canal de entrenamiento en Python: formato de puntos clave, segmentación, modelo Transformer, evaluación con firmantes no vistos, exportación ONNX, pruebas de paridad y canal de videos (de un video con permiso a muestras de entrenamiento). | [ml/README.md](ml/README.md) |
| `documentacion/` | Requisitos y su estado, arquitectura, protocolo de recolección con la comunidad y plantilla de consentimiento. | [documentacion/](documentacion/) |
| `herramientas/` | Verificación del código C# sin Unity (pruebas del núcleo y compilación de todos los scripts). | [herramientas/README.md](herramientas/README.md) |

## Cómo encaja todo

```
            ┌────────────────── App Unity (teléfono o tableta) ──────────────────┐
cámara ──► MediaPipe holístico ──► puntos LSRD-LM v1 ──► segmentador ──► modelo de señas ──► frase
                                                                       (Inference Engine)      │
micrófono ──► WAV ─────────────────────────────┐                                              │
            └──────────────────────────────────┼──────────────────────────────────────────────┼─┘
                                               ▼                                              ▼
                                  servidor: voz→texto→glosas                    servidor: glosas→español
                                               │                                              │
                                     avatar firma el plan                         voz sintetizada al empleado
```

Sin servidor la app sigue funcionando con traducción literal local y frases rápidas.

## Empezar

0. **App web** (Node 20+): `cd web && npm install && npm test && npm run build`.

1. **Probar el canal de entrenamiento** (Python 3.10+):
   ```bash
   cd ml && pip install -r requirements.txt
   python -m pytest -q                          # 48 pruebas
   python scripts/demo_sintetico.py --unity     # entrena y exporta los modelos de demostración
   ```
2. **Levantar el servidor**:
   ```bash
   cd servidor && pip install -r requirements.txt
   python -m pytest -q                          # 21 pruebas
   uvicorn app.main:app --host 0.0.0.0 --port 8000
   ```
3. **Abrir la app** en Unity 6: ver [unity/LSRD-AR/README.md](unity/LSRD-AR/README.md). Sin instalar MediaPipe ya se puede pulsar Play: el detector simulado reproduce grabaciones sintéticas y el modelo de demostración las reconoce.

## Qué se verificó (sin abrir Unity)

- App web: 24 pruebas del núcleo con paridad exacta frente a Python, y una prueba de punta a punta en Chromium con teléfono simulado (enseñar, reconocer, conversar, emergencia, guardar y recuperar, uso sin conexión). MediaPipe se comprobó dentro de la app con una imagen real; no con manos firmando.

- 48 pruebas del canal de entrenamiento, incluidos el canal de videos, el modo semilla y la exportación ONNX (opset 15) con diferencia máxima de 3·10⁻⁷ frente a PyTorch.
- 21 pruebas del servidor (emergencias, diccionario, reportes, traducción, memoria validada, validación de la salida del modelo de lenguaje, voz, grabaciones, avatar, clave de acceso y rutas maliciosas).
- 24 pruebas del núcleo C# de la app ejecutadas con .NET, con **paridad exacta Python ↔ C#** en remuestreo, segmentación, selección de mano y empaquetado de MediaPipe.
- Compilación de todos los scripts de Unity en tres variantes (editor, Android, iOS sin paquetes) contra las firmas de Unity 6, MediaPipe Unity Plugin 0.16.3 e Inference Engine 2.6.
- Flujo completo simulado: 12 señas sintéticas de firmantes nunca vistos → segmentador → modelo de demostración: 12 de 12 correctas.

Lo que **no** se pudo verificar aquí: la app web en un teléfono real con una persona firmando (velocidad, luz, manos que se tapan). Y en Unity/dispositivo: la cámara real, MediaPipe en el teléfono, el rendimiento, la voz del sistema y el aspecto final de la interfaz.

## Entrenar con videos

`fuentes/fuentes.csv` lista de dónde pueden salir videos de LSRD y si hay permiso para usarlos. El Diccionario Oficial en línea (unas 1.466 señas) es la mejor fuente, pero tiene derechos reservados del CONADIS: queda bloqueada hasta que exista autorización escrita. Con el permiso resuelto:

```bash
cd ml
python scripts/videos.py fuentes        # qué se puede usar
python scripts/videos.py recolectar     # descarga e inventario
python scripts/videos.py proponer --modelos-mediapipe modelos_mp/   # corta cada video en señas y sugiere la glosa
# una persona revisa datos/videos/anotaciones.csv
python scripts/videos.py muestras
python scripts/entrenar.py --datos datos/extraidos --salida modelos/semilla --semilla --unity
```

Un diccionario tiene una o dos personas por seña, así que de ahí sale un **modelo semilla**: útil para arrancar y para animar el avatar, no validado con personas nuevas. La app lo muestra como «MODELO SEMILLA».

## Próximos pasos (Fase 0 del plan)

1. Convenio con ANSORDO y CONADIS: autorización para usar los videos del Diccionario Oficial y validación del vocabulario y las frases con intérpretes.
2. Sesiones de grabación con al menos 30 firmantes sordos de varias regiones (modo recolección, protocolo en `documentacion/`).
3. Entrenar con datos reales, evaluar con firmantes no vistos (meta ≥ 85 %) y publicar grabaciones de referencia para el avatar.
4. Piloto en un mostrador real.

> Esta herramienta apoya la comunicación cotidiana y **no reemplaza a un intérprete de LSRD**, especialmente en trámites legales, médicos o judiciales.
