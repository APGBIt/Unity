# Protocolo de recolección de datos con la comunidad sorda

Objetivo de la fase 1: **≥ 30 firmantes sordos** de varias regiones, edades y manos dominantes; **5 tomas por seña** del vocabulario de prioridad 1; más tomas de "movimientos naturales" (clase de fondo).

## Antes de empezar

- Convenio firmado con ANSORDO (y CONADIS si se usa su diccionario como referencia).
- Vocabulario revisado por un comité con personas sordas: qué seña se graba para cada glosa y qué variantes regionales existen (`vocabulario/README.md`).
- Consentimiento presentado **en LSRD** por un intérprete, con la plantilla de `documentacion/CONSENTIMIENTO.md`. Se registra el código del participante, nunca el nombre en la app.
- Pago justo por sesión, acordado con la asociación.
- Servidor de la sesión encendido en la misma red (`servidor/README.md`) o tomas guardadas en el teléfono para enviar después.

## Montaje

- Teléfono o tableta en trípode, a la altura del pecho, a 1,2–1,5 m; la persona visible de la cintura a la cabeza con espacio sobre la cabeza.
- Luz de frente, fondo liso si es posible (no es obligatorio).
- Usar la cámara frontal para que la persona vea en pantalla la seña que debe hacer.
- Variar entre sesiones: teléfonos distintos, lugares, ropa y luz. La diversidad hace robusto al modelo.

## Sesión (45–60 minutos)

1. *Recolección de datos* en la app: consentimiento, perfil (región, mano dominante, edad, identidad, años de LSRD) y conjunto de señas.
2. Para cada glosa la persona hace la seña **con naturalidad** y luego baja las manos. La app detecta la seña y la muestra; quien coordina pulsa *Aceptar* o *Repetir*.
3. Al inicio y al final, la app pide 2,5 s de **movimientos naturales sin señar**.
4. Pausas cada 15 minutos. No forzar repeticiones si la persona está cansada.
5. Al final, *Enviar grabaciones pendientes*.

## Control de calidad

- Revisar en el servidor el avance: `GET /v1/recolecciones/resumen` (tomas y firmantes por glosa).
- Una persona sorda revisa una muestra de las tomas; las mal hechas se mueven fuera de la carpeta o se marcan `"revision": "rechazada"`.
- Entrenar con `ml/scripts/entrenar.py`; revisar en el reporte las clases débiles y las confusiones, y planificar nuevas tomas para esas señas.

## Privacidad

- La app no guarda video ni nombres. Las tomas contienen puntos clave y un perfil seudónimo.
- Los archivos del servidor se respaldan cifrados y solo el equipo autorizado accede.
- Si una persona pide retirar sus datos, se borra su carpeta y se vuelve a entrenar el modelo sin ellos.

## Grabaciones para el avatar

Entre las tomas de personas que aceptaron el uso para el avatar, el comité elige la mejor de cada seña y se publica con `servidor/publicar_avatar.py`. La app la descarga en *Ajustes › Descargar avatar*.
