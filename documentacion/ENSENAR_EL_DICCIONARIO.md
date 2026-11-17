# Cómo dejar las señas del diccionario incluidas en la app

Objetivo: que cualquiera que abra la app encuentre las señas ya enseñadas, sin grabar nada.

La app trae un «paquete base» (`web/paquetes/base.json`, publicado en `docs/paquetes/base.json`). Todo lo que esté ahí lo reconoce cualquier teléfono desde el primer momento. Hoy está vacío. Hay dos formas de llenarlo.

## Vía 1 — Una persona sorda o un intérprete enseña las señas (recomendada)

No requiere permiso de nadie más: una seña no tiene dueño; lo protegido son los videos del diccionario.

1. En un teléfono, abrir la app → **Enseñar señas**.
2. Elegir una palabra. Si la seña está en el Diccionario Oficial, la pantalla trae el enlace para verla ahí.
3. Tocar **Grabar ejemplos** y hacer la seña cinco veces, bajando las manos entre una y otra.
4. Probarla ahí mismo y borrar los ejemplos que hayan salido mal.
5. Al terminar la sesión: **Ajustes → Guardar en un archivo**, marcando la autorización.
6. Enviar el archivo. En el repositorio:

   ```bash
   cd web
   node herramientas/unir_paquetes.mjs --autor "Nombre o institución" archivo1.json archivo2.json
   npm run build
   ```

   y publicar. Los archivos sin autorización se rechazan.

A unos 40 segundos por seña, las 155 del vocabulario de atención al público son alrededor de dos horas de una persona. Con dos o tres personas distintas la app reconoce mejor a quien no participó.

## Vía 2 — A partir de videos

En **Enseñar señas**, el botón **Aprender de un video** lee un archivo de video en el teléfono, corta las señas y las guarda como ejemplos. Sirve con videos propios o con videos que se tenga permiso de usar.

Para usar los del Diccionario Oficial hace falta la autorización escrita del CONADIS (ver `fuentes/README.md`). Aun con ella, hay dos límites:

- Un video muestra la seña una o dos veces, y la app necesita tres ejemplos para reconocerla. Habría que completar con grabaciones en vivo.
- Todos los videos son de las mismas pocas personas, así que reconocerá peor a personas distintas.

## Qué palabras faltan

El vocabulario de la app tiene 193 entradas; el Diccionario Oficial, unas 1.466. Las que no estén se añaden desde **Enseñar señas → Añadir una palabra que no está**, y viajan dentro del archivo de señas.

## Qué revisar antes de publicar un paquete

- Que cada grabación sea la seña de LSRD que dice ser (revisión de una persona sorda o un intérprete).
- Que quien grabó haya autorizado la publicación: el paquete queda a la vista de cualquiera en el repositorio. Contiene puntos del cuerpo y las manos, no video.
