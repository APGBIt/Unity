# LSRD · Traductor de Lengua de Señas Dominicana para atención al público

Prototipo de app (Unity, Android/iOS) que traduce en las dos direcciones en un mostrador de atención al público:

- **Persona sorda → empleado:** la cámara lee la LSRD, la app arma la frase y la dice en voz alta en español dominicano.
- **Empleado → persona sorda:** el empleado habla (o escribe), el servidor transcribe y traduce a glosas LSRD, y un avatar las firma.

> **Estado honesto.** El software está completo de punta a punta y probado, pero **todavía no reconoce LSRD real**: no existe un conjunto de datos público de LSRD. Los modelos incluidos son de **demostración**, entrenados con datos sintéticos (clases `DEMO-01…DEMO-20`), para probar cámara, segmentación, inferencia e interfaz. El reconocimiento real llega cuando se graben personas sordas con el **modo recolección** de la app y se entrene con `ml/scripts/entrenar.py`. El avatar muestra tarjetas con la palabra hasta que existan grabaciones de referencia de firmantes nativos.

## Estructura

| Carpeta | Qué hay | Guía |
| --- | --- | --- |
| `vocabulario/` | Vocabulario del MVP (155 señas, 11 números, 27 letras) y 50 frases de mostrador. Todo marcado *pendiente de validación* por la comunidad sorda. | [vocabulario/README.md](vocabulario/README.md) |
| `unity/LSRD-AR/` | La app: cámara, detector MediaPipe holístico, reconocimiento con Unity Inference Engine, interfaz, avatar, modo recolección. | [unity/LSRD-AR/README.md](unity/LSRD-AR/README.md) |
| `servidor/` | API FastAPI: traducción glosas ↔ español (memoria validada → Claude opcional → literal), voz a texto (Whisper opcional), recepción de grabaciones y biblioteca del avatar. | [servidor/README.md](servidor/README.md) |
| `ml/` | Canal de entrenamiento en Python: formato de puntos clave, segmentación, modelo Transformer, evaluación con firmantes no vistos, exportación ONNX y pruebas de paridad. | [ml/README.md](ml/README.md) |
| `docs/` | Arquitectura, protocolo de recolección con la comunidad y plantilla de consentimiento. | [docs/](docs/) |
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

1. **Probar el canal de entrenamiento** (Python 3.10+):
   ```bash
   cd ml && pip install -r requirements.txt
   python -m pytest -q                          # 32 pruebas
   python scripts/demo_sintetico.py --unity     # entrena y exporta los modelos de demostración
   ```
2. **Levantar el servidor**:
   ```bash
   cd servidor && pip install -r requirements.txt
   python -m pytest -q                          # 17 pruebas
   uvicorn app.main:app --host 0.0.0.0 --port 8000
   ```
3. **Abrir la app** en Unity 6: ver [unity/LSRD-AR/README.md](unity/LSRD-AR/README.md). Sin instalar MediaPipe ya se puede pulsar Play: el detector simulado reproduce grabaciones sintéticas y el modelo de demostración las reconoce.

## Qué se verificó (sin abrir Unity)

- 32 pruebas del canal de entrenamiento, incluida la exportación ONNX (opset 15) con diferencia máxima de 3·10⁻⁷ frente a PyTorch.
- 17 pruebas del servidor (traducción, memoria validada, validación de la salida del modelo de lenguaje, voz, grabaciones, avatar, clave de acceso y rutas maliciosas).
- 20 pruebas del núcleo C# de la app ejecutadas con .NET, con **paridad exacta Python ↔ C#** en remuestreo, segmentación, selección de mano y empaquetado de MediaPipe.
- Compilación de todos los scripts de Unity en tres variantes (editor, Android, iOS sin paquetes) contra las firmas de Unity 6, MediaPipe Unity Plugin 0.16.3 e Inference Engine 2.6.
- Flujo completo simulado: 12 señas sintéticas de firmantes nunca vistos → segmentador → modelo de demostración: 12 de 12 correctas.

Lo que **no** se pudo verificar aquí y hay que probar en Unity/dispositivo: la cámara real, MediaPipe en el teléfono, el rendimiento, la voz del sistema y el aspecto final de la interfaz.

## Próximos pasos (Fase 0 del plan)

1. Convenio con ANSORDO y CONADIS; validar el vocabulario y las frases con intérpretes.
2. Sesiones de grabación con al menos 30 firmantes sordos de varias regiones (modo recolección, protocolo en `docs/`).
3. Entrenar con datos reales, evaluar con firmantes no vistos (meta ≥ 85 %) y publicar grabaciones de referencia para el avatar.
4. Piloto en un mostrador real.

> Esta herramienta apoya la comunicación cotidiana y **no reemplaza a un intérprete de LSRD**, especialmente en trámites legales, médicos o judiciales.
