# App Unity · LSRD Atención

## Requisitos

- **Unity 6** (6000.0 LTS o superior) con los módulos de compilación para Android y/o iOS.
- **Unity Inference Engine** (`com.unity.ai.inference`, ya listado en `Packages/manifest.json`). Si Package Manager no encuentra la versión 2.6.1, instale la que ofrezca (2.2 o superior).
- **MediaPipe Unity Plugin 0.16.3** (homuler). No se instala solo: descargue `com.github.homuler.mediapipe-0.16.3.tgz` de <https://github.com/homuler/MediaPipeUnityPlugin/releases> y agréguelo con *Window › Package Manager › + › Add package from tarball*. Ese paquete trae las bibliotecas nativas y el modelo `holistic_landmarker.bytes`.

Sin MediaPipe la app compila y funciona con el **detector simulado** (reproduce grabaciones sintéticas). Sin Inference Engine compila pero no reconoce señas.

## Primeros pasos

1. Abra la carpeta `unity/LSRD-AR` con Unity Hub (*Add project from disk*). La primera vez Unity genera `ProjectSettings` y descarga paquetes.
2. Instale MediaPipe (arriba), si lo tiene.
3. Menú **LSRD › Preparar proyecto**. Copia el modelo holístico, el vocabulario, configura el reproductor (IL2CPP/ARM64, permisos de cámara y micrófono, HTTP local) y crea la escena `Assets/LSRD/Escenas/LSRD.unity`.
4. Pulse **Play**. En el menú de la app entre a *Conversación*. Con el detector simulado verá una figura que hace señas `DEMO-xx` y la app las reconoce y arma frases.
5. En *Ajustes* escriba la dirección del servidor (por ejemplo `http://192.168.1.10:8000`) y pulse *Probar conexión*.

## Pruebas dentro de Unity

*Window › General › Test Runner › EditMode › Run All*:

- `PruebasNucleo`: paridad con Python (remuestreo, segmentación, empaquetado), cámara, frases, dactilología, audio, JSON y traducción local.
- `PruebasModelo`: compara las probabilidades de Inference Engine con ONNX Runtime para los modelos de `Resources/LSRD/Modelos`. Si reemplaza los modelos, regenere los vectores con `python ml/scripts/generar_vectores_oro.py`.

## Compilar para Android

1. *File › Build Profiles › Android › Switch Platform*.
2. MediaPipe necesita `libc++_shared.so` en el APK: copie el archivo del NDK de Unity (`…/NDK/toolchains/llvm/prebuilt/<sistema>/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so`) a `Assets/Plugins/Android/libs/arm64-v8a/`, o use el bloque de `mainTemplate.gradle` del README del plugin (sección *For Android*).
3. *Build And Run* con un teléfono conectado (Android 8 o superior, ARM64).

Para iOS, compile el proyecto de Xcode; la voz usa `Assets/LSRD/Plugins/iOS/LSRDVoz.mm` (AVSpeechSynthesizer).

## Cómo está organizado

| Carpeta | Contenido |
| --- | --- |
| `Scripts/Nucleo` | Lógica sin Unity (probada con .NET y con Python): formato de puntos, remuestreo, segmentación, armado de frases, dactilología, audio, JSON, traducción local. `FormatoLandmarks.g.cs` se genera desde Python. |
| `Scripts/App` | Cámara (`FuenteCamara`), detectores (`DetectorHolistico`, `DetectorSimulado`), modelos (`ClasificadorOnnx`, `MotorReconocimiento`), servidor (`ClienteLSRD`), micrófono y voz, avatar, recolección e interfaz construida por código. |
| `Scripts/Editor` | Menú **LSRD**. |
| `Resources/LSRD` | Modelos ONNX, vocabulario, grabaciones de simulación, sombreador de cámara y (tras preparar) el modelo de MediaPipe. |
| `Tests/EditMode` | Pruebas y vectores de oro. |

## Modos de la app

- **Conversación.** El dispositivo mira a la persona sorda. Lo que firma aparece junto a su cabeza (realidad aumentada) y abajo, con la confianza; las señas dudosas muestran alternativas para tocar. Al pausar, la app traduce y lo dice en voz alta. El empleado mantiene *Mantener para hablar* (o activa la escucha automática), escribe, o usa frases rápidas; el avatar firma la respuesta. *Deletreo* activa el alfabeto manual.
- **Emergencia.** Botón rojo en el inicio y en la conversación. La persona sorda toca una frase («Necesito ayuda urgente», «Llame al 9-1-1»…) y el teléfono la muestra en letras grandes y la dice en voz alta. Funciona sin servidor, sin cámara y sin modelo.
- **Diccionario.** Búsqueda por palabra o por tema entre las 193 fichas del vocabulario. Cada ficha indica si la seña está validada, si aparece en el Diccionario Oficial (con enlace) y reproduce la seña cuando hay una grabación aprobada.
- **Reportar.** En la conversación y en cada ficha. Envía solo el texto de la última traducción y lo que la persona quiso decir; nunca audio ni video. Sin conexión queda guardado y se envía después.
- **Recolección de datos.** Consentimiento, perfil seudónimo y grabación guiada seña por seña, con revisión de cada toma. Solo guarda puntos clave (no video). Las tomas se envían al servidor cuando hay conexión.
- **Ajustes.** Servidor y clave, cámara (frontal, espejo, rotación), umbrales, voz y velocidad del avatar.

## Avatar

- **Esquemático (incluido).** Reproduce grabaciones reales de firmantes sordos que aceptaron ese uso. Se publican en el servidor con `servidor/publicar_avatar.py` y la app las descarga en *Ajustes › Descargar avatar*. Mientras una seña no tenga grabación, se muestra una tarjeta con la palabra.
- **3D (opcional).** Coloque un prefab humanoide en `Resources/LSRD/AvatarHumanoide` y una lista *Assets › Create › LSRD › Clips de señas* llamada `ClipsDeSenas` en `Resources/LSRD`, con un clip de captura de movimiento por glosa. La app lo usa automáticamente para las señas que tengan clip.

## Problemas frecuentes

- **La imagen sale girada.** *Ajustes › Rotación de la imagen*. Active *Dibujar los puntos clave* para comprobar que la figura coincide con la persona.
- **"Sin servidor".** Teléfono y computadora deben estar en la misma red; abra el puerto 8000 en el firewall.
- **`DllNotFoundException` de MediaPipe en Android.** Falta `libc++_shared.so` (paso 2 de Android).
