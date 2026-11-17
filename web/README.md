# App web Señas RD

App instalable para el teléfono: traduce señas a voz y voz a señas con la cámara, sin instalar nada desde una tienda. Se publica con GitHub Pages desde la carpeta `docs/` del repositorio.

**Abrirla:** https://apgbit.github.io/Unity/

## Instalar en el celular

1. Abra el enlace en el celular.
2. **Android (Chrome):** menú ⋮ → «Instalar app».
3. **iPhone (Safari):** botón Compartir → «Agregar a inicio».

La primera vez que abre la cámara descarga el detector (unos 25 MB). Después funciona sin conexión.

## Cómo reconoce las señas

No existe todavía un conjunto público de grabaciones de LSRD con el que entrenar un modelo. Por eso la app aprende **por ejemplos**: cada persona le enseña sus señas en «Enseñar señas», tres veces cada una, y desde ese momento las reconoce.

- Compara cada seña nueva con los ejemplos guardados, alineándolas en el tiempo (una puede ser más rápida que otra) y midiendo la posición y la forma de las manos respecto al cuerpo.
- No depende de la distancia a la cámara ni del encuadre, y reconoce también a quien firma con la otra mano.
- Cuando una seña no se parece lo suficiente a ninguna de las enseñadas, no responde: nunca inventa una seña.
- Las señas enseñadas se pueden guardar en un archivo y cargar en otro teléfono.

Límites que hay que conocer:

- Reconoce **señas sueltas con una pausa breve** (manos abajo) entre una y otra, no firmado continuo.
- La traducción a español es **palabra por palabra**; no aplica la gramática de la LSRD.
- Las señas son las que cada persona enseñó: no son una referencia oficial.
- Todavía no usa la expresión de la cara para distinguir señas.

## Medición

Con datos **sintéticos** (no son LSRD; sirven para comprobar que el método funciona), 12 señas y 4 personas:

| Caso | Acierta | Se equivoca | No responde | Acepta señas no enseñadas |
| --- | --- | --- | --- | --- |
| La misma persona, 3 ejemplos por seña | 91,0 % | 0,0 % | 9,0 % | 8,3 % |
| La misma persona, 5 ejemplos por seña | 97,9 % | 0,0 % | 2,1 % | 2,1 % |
| Otra persona (enseñó una sola, 5 ejemplos) | 94,9 % | 0,0 % | 5,1 % | 2,1 % |
| Otra persona (enseñaron tres, 3 ejemplos cada una) | 96,9 % | 0,0 % | 3,1 % | 0,0 % |

Con señas reales los números serán distintos, y lo esperable es que bajen: las manos reales se tapan entre sí y el detector se equivoca más. Para repetir la medición:

```bash
python ../ml/scripts/exportar_datos_web.py --grande /tmp/sintetico_grande.json.gz
node tests/evaluar.mjs /tmp/sintetico_grande.json.gz
```

## Desarrollo

```bash
npm install
npm test            # 22 pruebas del núcleo (incluye paridad exacta con Python y C#)
npm run build       # genera ../docs
npm run e2e         # prueba de punta a punta en Chromium con una cámara simulada (necesita Playwright)
```

| Carpeta | Contenido |
| --- | --- |
| `src/nucleo/` | Lógica sin navegador: formato de puntos clave (generado desde `ml/lsrd_ml/formato.py`), segmentación, rasgos, reconocedor por ejemplos, vocabulario, frases y paquete de señas. |
| `src/app/` | Cámara y MediaPipe, voz, almacenamiento en el teléfono, dibujo del avatar e instalación. |
| `src/ui/` | Pantallas: inicio, conversación, enseñar, emergencia, diccionario y ajustes. |
| `tests/` | Pruebas del núcleo y medición del reconocedor. |
| `e2e/` | Prueba de punta a punta. |

`node e2e/run.mjs camara imagen.y4m` comprueba que MediaPipe arranca dentro de la app con una imagen real como cámara.

## Privacidad

- La cámara se analiza en el teléfono. El video no se guarda ni se envía.
- De cada seña enseñada se guardan solo puntos del cuerpo y las manos, en el teléfono.
- Las conversaciones no se guardan.
- Si se usa el micrófono, el navegador transcribe la voz con su propio servicio (en Chrome, de Google).

## Aportar señas al entrenamiento

Al guardar las señas en un archivo, la persona puede autorizar su uso para entrenar. `ml/scripts/importar_paquete_web.py` convierte esos archivos en muestras para `entrenar.py`; los que no traen autorización se rechazan.
