import { h, boton, poner } from './dom.js';
import { app, senasEmpezadas, senasListas } from '../app/estado.js';
import { alCambiarInstalacion, estadoInstalacion, pedirInstalacion } from '../app/pwa.js';

export function pantallaInicio(raiz, ir) {
  const listas = senasListas(), empezadas = senasEmpezadas();
  const instalar = h('div');
  const pintarInstalar = () => {
    const e = estadoInstalacion();
    if (e.instalada) { poner(instalar); return; }
    if (e.puedePedir) poner(instalar, h('div', { class: 'tarjeta fila-entre' }, h('p', { text: 'Instale la app para abrirla desde la pantalla de inicio, también sin conexión.' }), boton('Instalar', pedirInstalacion, 'primario chico')));
    else poner(instalar, h('p', { class: 'nota', text: e.ios ? 'Para instalarla en iPhone: botón Compartir → «Agregar a inicio».' : 'Para instalarla: menú ⋮ del navegador → «Instalar app» o «Agregar a pantalla de inicio».' }));
  };
  pintarInstalar();
  const quitar = alCambiarInstalacion(pintarInstalar);

  poner(raiz,
    h('main', { class: 'inicio' },
      h('div', { class: 'marca' }, h('div', { class: 'logo', 'aria-hidden': 'true' }, '🤟'), h('div', null, h('h1', { text: 'Señas RD' }), h('p', { text: 'Lengua de Señas Dominicana ↔ español' }))),
      h('div', { class: 'tarjeta estado' + (listas ? '' : ' vacia') },
        listas
          ? [h('strong', { text: `${listas} ${listas === 1 ? 'seña lista' : 'señas listas'} para reconocer` }), app.baseInfo && app.ajustes.usarBase !== false ? h('span', { text: `${app.baseInfo.senas} vienen incluidas con la app` }) : null, empezadas > listas ? h('span', { text: `${empezadas - listas} más a medio enseñar` }) : null]
          : [h('strong', { text: 'Todavía no reconoce ninguna seña' }), h('span', { text: 'Enséñele las suyas: tres ejemplos de cada una bastan para empezar.' })]),
      boton(h('span', null, 'Conversación', h('small', { text: 'Señas a voz y voz a señas' })), () => ir('conversacion'), 'primario grande'),
      boton(h('span', null, 'Emergencia', h('small', { text: 'Frases urgentes, sin conexión' })), () => ir('emergencia'), 'peligro grande'),
      h('div', { class: 'rejilla' },
        boton('Enseñar señas', () => ir('ensenar'), 'suave alto'),
        boton('Diccionario', () => ir('diccionario'), 'suave alto'),
        boton('Ajustes', () => ir('ajustes'), 'suave alto')),
      instalar,
      h('p', { class: 'nota', text: 'Esta herramienta apoya la comunicación cotidiana. No reemplaza a un intérprete de LSRD, sobre todo en trámites legales, médicos o judiciales.' })));

  return quitar;
}
