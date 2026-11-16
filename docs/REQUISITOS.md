# Requisitos del producto «Señas RD» y su estado

Este documento adopta la especificación funcional del producto (visión, módulos, requisitos RF/RNF, reglas de negocio y criterios del MVP) y dice, para cada punto, qué existe hoy en el repositorio.

**Estados:** `Hecho` = implementado y con pruebas automáticas. `Parcial` = existe una parte; se indica cuál falta. `Pendiente` = no empezado. Ningún requisito que dependa de la cámara, el micrófono o la voz del teléfono se ha probado todavía en un dispositivo real.

## Visión y principio rector

Que una persona sorda pueda comunicarse directamente con cualquier persona sin que la falta de un intérprete sea una barrera. La app traduce significados, no sustituye palabras por gestos; muestra su nivel de confianza, permite corregir y reconoce cuándo hace falta un intérprete humano. En servicios médicos, jurídicos y otras situaciones críticas no se presenta como sustituto de un intérprete profesional.

## Decisiones que se apartan de la especificación

| Tema | Especificación | Decisión | Motivo |
| --- | --- | --- | --- |
| App móvil | Flutter | **Unity** (Android e iOS) | La especificación ya pide Unity para el avatar; una sola base de código para cámara, reconocimiento y avatar. |
| Web | React / Next.js | Más adelante, para el panel administrativo y el diccionario | El reconocimiento en el dispositivo es lo crítico del MVP. |
| Vocabulario del piloto | 300 a 500 unidades en 8 temas | 155 señas de atención al público, números, alfabeto y 16 frases de emergencia | Cada seña nueva hay que grabarla con unas 30 personas. Se amplía por temas cuando el primero esté validado. |
| Base de datos | PostgreSQL | Archivos versionados (CSV y JSON) | Suficiente hasta que existan cuentas de usuario y panel. |
| Tiempo real | WebSocket / WebRTC | Peticiones por turno | La conversación del MVP es por turnos alternos. |

## Módulos

| # | Módulo | Estado | Dónde está y qué falta |
| --- | --- | --- | --- |
| 4.1 | Voz a LSRD | Parcial | `Microfono.cs`, `servidor/app/voz.py`, `traductor.py`, `ReproductorPlan.cs`. Falta: glosas validadas por intérpretes y grabaciones para el avatar. |
| 4.2 | LSRD a voz y texto | Parcial | `DetectorHolistico.cs`, `Segmentacion.cs`, `MotorReconocimiento.cs`, `VozSintetizada.cs`. Falta: modelo entrenado con LSRD real. |
| 4.3 | Conversación bidireccional | Hecho | `PantallaConversacion.cs`: turnos alternos, subtítulos, confianza, repetir, velocidad. No detecta sola quién se comunica: el empleado mantiene pulsado para hablar o activa la escucha automática. |
| 4.4 | Avatar intérprete | Parcial | Reproduce únicamente grabaciones aprobadas (`BibliotecaSenas.cs`); sin grabación muestra la palabra escrita. El avatar 3D con esqueleto (`AvatarHumanoide.cs`) está preparado y necesita un modelo 3D. |
| 4.5 | Diccionario LSRD | Parcial | `PantallaDiccionario.cs`, `Diccionario.cs`, `GET /v1/diccionario`: 193 fichas con búsqueda, tema, estado de validación y referencia al Diccionario Oficial. Faltan video de referencia y parámetros manuales y no manuales: dependen del convenio con CONADIS. |
| 4.6 | Usuarios y perfiles | Pendiente | Hoy solo hay un perfil seudónimo del firmante en el modo recolección y una clave de acceso al servidor. |
| 4.7 | Aprendizaje | Pendiente | El diccionario es la base. |
| 4.8 | Escenarios | Parcial | Atención al público (50 frases) y emergencias (16). Salud, educación, banca, transporte y jurídico: pendientes. |
| 4.9 | Retroalimentación | Hecho | Botón «Reportar» en la conversación y en cada ficha; `Retroalimentacion.cs`; `servidor/app/retroalimentacion.py`. Los reportes quedan pendientes de revisión y no cambian el diccionario ni entrenan modelos. |
| 4.10 | Administración y analítica | Parcial | Por API: avance de la recolección, lista y revisión de reportes. Falta el panel web, las cuentas y la auditoría. |

## Requisitos funcionales

| Código | Requisito | Estado | Nota |
| --- | --- | --- | --- |
| RF-001 | Registrar e iniciar sesión | Pendiente | |
| RF-002 | Seleccionar el modo de traducción | Hecho | Menú principal; la conversación reúne las dos direcciones. |
| RF-003 | Capturar audio | Hecho | Pulsar para hablar o escucha automática. |
| RF-004 | Voz a texto | Hecho | En el servidor, con Whisper (opcional). |
| RF-005 | Interpretar el contexto | Parcial | Memoria de frases validadas, luego modelo de lenguaje (opcional), luego traducción literal marcada como no validada. |
| RF-006 | Secuencia equivalente en LSRD | Parcial | Las reglas de orden son hipótesis a validar con especialistas; ninguna frase tiene glosas validadas todavía. |
| RF-007 | Avatar 3D | Parcial | Avatar esquemático con grabaciones aprobadas; el 3D necesita modelo. |
| RF-008 | Capturar video | Hecho | |
| RF-009 | Reconocer manos, rostro y cuerpo | Hecho | 78 puntos por cuadro (MediaPipe holístico). |
| RF-010 | Señas continuas | Parcial | Señas con una breve pausa entre ellas. El reconocimiento continuo es una fase posterior. |
| RF-011 | Señas a texto | Parcial | El software está completo; el modelo incluido es de demostración. |
| RF-012 | Texto a voz | Hecho | Voz del sistema (Android e iOS). |
| RF-013 | Conversación bidireccional | Hecho | |
| RF-014 | Pausar, repetir y cambiar la velocidad | Parcial | Repetir y velocidad. Falta pausar. |
| RF-015 | Mostrar la confianza | Hecho | Barra de confianza y etiqueta «validada / no validada». |
| RF-016 | Confirmar o corregir | Hecho | Alternativas por seña, borrar, enviar y reportar. |
| RF-017 | Diccionario LSRD | Hecho | Ver módulo 4.5. |
| RF-018 | Historial con consentimiento | Pendiente | Por diseño la app no guarda conversaciones. |
| RF-019 | Gestionar señas y contenidos | Parcial | Archivos versionados y `publicar_avatar.py`; sin panel. |
| RF-020 | Reportes de calidad y uso | Parcial | Resúmenes por API y reportes de entrenamiento; sin tablero. |

## Requisitos no funcionales

| Código | Requisito | Estado | Nota |
| --- | --- | --- | --- |
| RNF-001 | Latencia menor de 2 s | Sin medir | Requiere dispositivo real. |
| RNF-002 | Disponibilidad 99,5 % | No aplica aún | No hay despliegue. |
| RNF-003 | Cifrado | Parcial | Clave de acceso en el servidor; HTTPS se configura en el despliegue. |
| RNF-004 | No guardar audio ni video por defecto | Hecho | Nunca se guardan. Del modo recolección salen solo puntos clave, con consentimiento. |
| RNF-005 | Accesibilidad | Parcial | Texto grande, alto contraste y vibración. Falta probar con lectores de pantalla. |
| RNF-006 | Android, iOS y navegadores | Parcial | Android e iOS compilan; no hay versión web. |
| RNF-007 | Escalabilidad | Pendiente | |
| RNF-008 | Usabilidad | Sin probar | Requiere sesiones con personas sordas. |
| RNF-009 | Mantenibilidad | Hecho | Módulos separados, pruebas y documentación. |
| RNF-010 | Trazabilidad | Parcial | Versión del vocabulario, ficha de cada modelo e historial del repositorio. Falta la auditoría administrativa. |
| RNF-011 | Métricas de precisión | Parcial | Evaluación separada por persona ya implementada; faltan datos reales. |
| RNF-012 | Resiliencia | Parcial | La app funciona sin servidor y reintenta los envíos pendientes. |

## Reglas de negocio

| # | Regla | Cómo se cumple |
| --- | --- | --- |
| 1 | No presentar como correcta una traducción bajo el umbral | Bajo la confianza mínima la seña se ignora; entre los dos umbrales se muestra con alternativas. |
| 2 | Una traducción incierta pide confirmar o repetir | Alternativas por seña y botón «Borrar». |
| 3 | Señas nuevas validadas antes de entrar | Columna `estado` del vocabulario; `entrenar.py --solo-validadas`. |
| 4 | El usuario controla la grabación | No se graba nada fuera del modo recolección, que exige consentimiento explícito. |
| 5 | Distinguir traducción automática de intérprete humano | Aviso permanente en el inicio y etiqueta en cada traducción. |
| 6 | El avatar usa movimientos validados | Solo reproduce grabaciones publicadas con consentimiento para avatar. |
| 7 | No entrenar con conversaciones sin consentimiento | Las conversaciones no se guardan; los reportes llevan solo texto y no alimentan el entrenamiento. |
| 8 | Acceso rápido a emergencias | Botón «Emergencia» en el inicio y en la conversación; funciona sin servidor, cámara ni modelo. |

## Criterios de aceptación del MVP

Se adoptan tal como están en la especificación: 100 % de cobertura del vocabulario aprobado, al menos 85 % de comprensión de frases de prueba, al menos 90 % en señas aisladas, 80 % de conversaciones sin asistencia técnica, 99 % de disponibilidad en el piloto, 85 % de satisfacción y ningún error crítico conocido sin advertencia. Todos se miden con personas que no participaron en el entrenamiento. Hoy ninguno se puede medir: faltan los datos reales de LSRD.
