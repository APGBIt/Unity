import { h, aviso, barra, boton, confirmar, descargar, poner } from './dom.js';
import { app, borrarTodo, exportarPaquete, guardarAjustes, importarPaquete, senasListas } from '../app/estado.js';
import { decir, puedeEscuchar, puedeHablar, voces } from '../app/voz.js';
import { estadoInstalacion, pedirInstalacion } from '../app/pwa.js';

const VERSION = typeof __VERSION__ === 'string' ? __VERSION__ : 'desarrollo';

export function pantallaAjustes(raiz, ir, volver) {
  const a = app.ajustes;
  const interruptor = (clave, titulo, ayuda) => h('label', { class: 'ajuste' },
    h('span', null, h('strong', { text: titulo }), ayuda ? h('small', { text: ayuda }) : null),
    h('input', { type: 'checkbox', checked: !!a[clave], onchange: (e) => guardarAjustes({ [clave]: e.target.checked }) }));
  const selector = (clave, titulo, opciones, ayuda, convertir = (v) => v) => h('label', { class: 'ajuste columna' },
    h('span', null, h('strong', { text: titulo }), ayuda ? h('small', { text: ayuda }) : null),
    h('select', { class: 'campo', onchange: (e) => guardarAjustes({ [clave]: convertir(e.target.value) }) },
      opciones.map(([v, t]) => h('option', { value: String(v), text: t, selected: String(a[clave]) === String(v) }))));

  const listaVoces = voces();
  const archivo = h('input', { type: 'file', accept: '.json,application/json', hidden: true, onchange: async (ev) => {
    const f = ev.target.files && ev.target.files[0];
    ev.target.value = '';
    if (!f) return;
    try {
      if (f.size > 80 * 1024 * 1024) throw new Error('El archivo es demasiado grande.');
      const r = await importarPaquete(JSON.parse(await f.text()));
      aviso(`${r.nuevos} ${r.nuevos === 1 ? 'ejemplo añadido' : 'ejemplos añadidos'}${r.errores.length ? `; ${r.errores.length} con errores se omitieron` : ''}.`);
      pintarDatos();
    } catch (e) { aviso(e instanceof SyntaxError ? 'El archivo no se pudo leer.' : e.message, 'alerta'); }
  } });

  async function exportar() {
    if (!app.ejemplos.length) { aviso('Todavía no hay señas enseñadas.'); return; }
    const casilla = h('input', { type: 'checkbox' });
    const ok = await confirmar({
      titulo: 'Guardar mis señas en un archivo',
      texto: 'El archivo contiene solo puntos del cuerpo y las manos; no contiene video. Sirve como copia de seguridad y para pasar sus señas a otro teléfono.',
      aceptar: 'Guardar archivo',
      contenido: h('label', { class: 'ajuste' }, h('span', null, h('small', { text: 'Autorizo que estas grabaciones se usen para entrenar el reconocimiento de LSRD, si entrego el archivo al proyecto.' })), casilla),
    });
    if (!ok) return;
    descargar(`senas-rd-${new Date().toISOString().slice(0, 10)}.json`, JSON.stringify(exportarPaquete(casilla.checked)));
  }

  async function vaciar() {
    const ok = await confirmar({ titulo: '¿Borrar todas las señas?', texto: 'Se borran todos los ejemplos y las palabras añadidas en este teléfono. Las señas incluidas con la app no se tocan. No se puede deshacer.', aceptar: 'Borrar todo', peligro: true });
    if (!ok) return;
    await borrarTodo();
    aviso('Se borraron las señas de este teléfono.');
    pintarDatos();
  }

  const datos = h('p', { class: 'nota' });
  const pintarDatos = () => { datos.textContent = `${app.ejemplos.length} ejemplos de ${new Set(app.ejemplos.map((e) => e.glosa)).size} señas enseñadas en este teléfono. En total, ${senasListas()} señas listas para reconocer. ` + (app.almacen.persistente ? 'Guardados en este teléfono.' : 'Este navegador no deja guardarlos: se pierden al cerrar.'); };
  pintarDatos();
  const inst = estadoInstalacion();

  poner(raiz, barra('Ajustes', volver),
    h('main', { class: 'pagina ajustes' },
      h('h2', { text: 'Mis señas' }), datos,
      h('div', { class: 'rejilla dos' }, boton('Guardar en un archivo', exportar, 'suave'), boton('Cargar un archivo', () => archivo.click(), 'suave')), archivo,
      h('label', { class: 'ajuste columna' }, h('span', null, h('strong', { text: 'Nombre o seudónimo' }), h('small', { text: 'Opcional. Se anota en el archivo de señas.' })),
        h('input', { type: 'text', class: 'campo', value: a.autor, maxlength: 60, onchange: (e) => guardarAjustes({ autor: e.target.value.trim() }) })),
      boton('Borrar todas las señas', vaciar, 'suave rojo'),
      app.baseInfo ? interruptor('usarBase', 'Usar las señas incluidas con la app', `${app.baseInfo.senas} señas ya vienen enseñadas${app.baseInfo.autor ? ` (por ${app.baseInfo.autor})` : ''}. Las suyas se suman a esas.`) : null,

      h('h2', { text: 'Reconocimiento' }),
      selector('sensibilidad', 'Exigencia', [['estricta', 'Estricta: se equivoca menos, pide repetir más'], ['normal', 'Normal'], ['tolerante', 'Tolerante: acepta señas menos parecidas']]),
      selector('pausaFraseMs', 'Pausa para terminar la frase', [[1200, 'Corta (1,2 s)'], [1800, 'Normal (1,8 s)'], [2600, 'Larga (2,6 s)']], 'Tiempo con las manos abajo para dar la frase por terminada.', Number),
      interruptor('otraMano', 'Reconocer también con la otra mano', 'Para personas zurdas o diestras, sin volver a enseñar.'),
      interruptor('detectorPreciso', 'Detector más preciso', 'Sigue mejor las manos, pero va más lento en teléfonos modestos.'),

      h('h2', { text: 'Voz' }),
      puedeHablar() ? null : h('p', { class: 'etiqueta pendiente', text: 'Este navegador no puede hablar en voz alta.' }),
      interruptor('vozActiva', 'Decir en voz alta'),
      interruptor('decirSola', 'Decir la frase al terminar de firmar', 'Si la apaga, hay que tocar «Decir».'),
      listaVoces.length ? selector('voz', 'Voz', [['', 'La mejor disponible'], ...listaVoces.map((v) => [v.name, `${v.name} (${v.lang})`])]) : null,
      selector('velocidadVoz', 'Velocidad de la voz', [[0.8, 'Lenta'], [1, 'Normal'], [1.2, 'Rápida']], null, Number),
      boton('Probar la voz', () => { if (!decir('Buenos días. ¿En qué puedo ayudarle?', { velocidad: a.velocidadVoz, voz: a.voz })) aviso('Este navegador no puede hablar.'); }, 'suave'),
      puedeEscuchar() ? null : h('p', { class: 'nota', text: 'Este navegador no transcribe la voz: quien atiende tendrá que escribir.' }),
      selector('velocidadAvatar', 'Velocidad de las señas del avatar', [[0.7, 'Lenta'], [1, 'Normal'], [1.3, 'Rápida']], null, Number),

      h('h2', { text: 'Instalar' }),
      inst.instalada ? h('p', { class: 'nota', text: 'La app ya está instalada en este teléfono.' })
        : inst.puedePedir ? boton('Instalar la app', pedirInstalacion, 'primario')
          : h('p', { class: 'nota', text: inst.ios ? 'En iPhone: abra la app en Safari, toque Compartir y luego «Agregar a inicio».' : 'En Android: menú ⋮ de Chrome → «Instalar app» o «Agregar a pantalla de inicio».' }),

      h('h2', { text: 'Privacidad' }),
      h('ul', { class: 'puntos' },
        h('li', { text: 'La cámara se analiza en el propio teléfono. El video no se guarda ni se envía.' }),
        h('li', { text: 'De cada seña enseñada se guardan solo puntos del cuerpo y las manos, en este teléfono.' }),
        h('li', { text: 'Las conversaciones no se guardan.' }),
        h('li', { text: 'Si usa el micrófono, el navegador transcribe la voz con su propio servicio (en Chrome, de Google). La app no guarda el audio.' })),

      h('h2', { text: 'Acerca de' }),
      h('p', { class: 'nota', text: `Señas RD ${VERSION}. Las señas las enseña cada persona en su teléfono: no son una referencia oficial de la LSRD. Esta herramienta no reemplaza a un intérprete.` }),
      h('p', { class: 'nota' }, 'Detección de manos y cuerpo: MediaPipe (Google, licencia Apache 2.0). Referencias: ', h('a', { href: 'https://diccionariolsrd.cc/', target: '_blank', rel: 'noopener', text: 'Diccionario Oficial de la LSRD' }), '.')));
  return () => {};
}
