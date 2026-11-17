# Arquitectura técnica

## Decisiones

| Tema | Decisión | Motivo |
| --- | --- | --- |
| Plataforma | App nativa Unity 6 (Android/iOS) | Un solo código, motor 3D para el avatar, inferencia en el dispositivo. |
| Detector | MediaPipe holístico (pose + manos + cara en una pasada), plugin homuler 0.16.3 | Corre en el teléfono; asigna mano izquierda/derecha por el cuerpo; incluye rasgos no manuales. |
| Representación | Puntos clave LSRD-LM v1, no píxeles | Liviano, privado (no se guarda video), robusto a fondo e iluminación. |
| Reconocimiento | Señas aisladas segmentadas por movimiento + Transformer pequeño | Alcanzable con cientos de tomas por seña; base para reconocimiento continuo (CTC) en la fase 2. |
| Inferencia | Unity Inference Engine, ONNX opset 15, CPU | Modelos de ~1 M de parámetros; sin GPU ni red. |
| Traducción | Servidor: memoria validada → Claude con vocabulario cerrado → literal | La gramática LSRD necesita contexto; las salidas se validan y se marcan. |
| Voz | Whisper (servidor) para el empleado; voz del sistema para la salida | Español con acento dominicano; sin dependencias en el teléfono. |
| Avatar | Grabaciones de firmantes nativos reproducidas como figura; 3D opcional | La comunidad rechaza avatares robóticos; las grabaciones reales conservan la seña auténtica. |

## Formato LSRD-LM v1

78 puntos por cuadro, en este orden:

| Índices | Grupo | Origen MediaPipe |
| --- | --- | --- |
| 0–6 | nariz, hombros, codos, muñecas | Pose 0, 11–16 |
| 7–27 | mano izquierda de la persona | 21 puntos de mano |
| 28–48 | mano derecha de la persona | 21 puntos de mano |
| 49–77 | boca (8), cejas (10), ojos (8), nariz, mentón, frente | Face Mesh |

Coordenadas: `x = x_norm · ancho/alto`, `y = y_norm`, `z = z_norm · ancho/alto`; ausente = (0, 0, 0). Los puntos de la pose con visibilidad < 0,5 se tratan como ausentes. La imagen que procesa el detector está derecha y **sin espejo**; el espejo solo se aplica al mostrarla.

La fuente de verdad es `ml/lsrd_ml/formato.py`; la app usa `FormatoLandmarks.g.cs`, generado desde ahí.

## Contratos de los modelos

| Archivo | Entrada | Salidas |
| --- | --- | --- |
| `senas.onnx` | `landmarks` `[1, 32, 78, 3]` (cuadros remuestreados por vecino más cercano) | `logits`, `probs` `[1, C]` |
| `dactilologia.onnx` | `mano` `[1, 21, 3]` (mano activa canonizada como derecha) | `logits`, `probs` `[1, L]` |
| `*_info.json` | clases, formato, longitud, métricas, nota (demostración o real) | — |

La normalización vive dentro del modelo: la app solo empaqueta puntos crudos.

## Flujo en la app

1. `FuenteCamara` endereza la imagen de la cámara en GPU (`CamaraNormalizada.shader`).
2. `DetectorHolistico` envía cada cuadro a MediaPipe (modo transmisión) y empaqueta el resultado en LSRD-LM v1.
3. `MotorReconocimiento` mide la actividad, el `Segmentador` detecta inicio y fin de cada seña (manos que bajan, quietud de las muñecas o 3 s máximo), recorta el segmento del `BufferCuadros`, lo remuestrea a 32 cuadros y lo clasifica.
4. `ConstructorFrase` descarta el fondo y lo muy inseguro, marca lo dudoso y cierra la frase tras 1,5 s sin señas.
5. `ClienteLSRD` traduce la frase; la app la muestra junto a la cabeza de la persona y la dice en voz alta.
6. En la otra dirección, `Microfono` entrega WAV al servidor, que responde con un plan (señas y deletreos) que `ReproductorPlan` muestra en el avatar.

## Pruebas de paridad

El núcleo de la app (`Scripts/Nucleo`) no depende de Unity. `ml/scripts/generar_vectores_oro.py` produce casos con la implementación de Python y las pruebas C# (en Unity y con .NET en `herramientas/`) exigen el mismo resultado: índices de remuestreo, actividad y velocidad cuadro a cuadro, segmentos, mano activa y empaquetado. `PruebasModelo` compara Inference Engine con ONNX Runtime.

## Extensiones previstas

- **Reconocimiento continuo** (fase 2): grabar frases completas, entrenar con CTC y emitir glosas sin pausas.
- **Variantes regionales:** etiquetar la región en cada toma (ya se guarda) y evaluar por región.
- **Avatar 3D:** retargetear grabaciones con captura de movimiento a un humanoide con rig facial (`AvatarHumanoide`).
