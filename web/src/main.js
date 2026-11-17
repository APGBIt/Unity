import { h, ocultarAviso, poner } from './ui/dom.js';
import { iniciar } from './app/estado.js';
import { iniciarPWA } from './app/pwa.js';
import { pantallaInicio } from './ui/inicio.js';
import { pantallaConversacion } from './ui/conversacion.js';
import { pantallaEmergencia } from './ui/emergencia.js';
import { pantallaDiccionario } from './ui/diccionario.js';
import { pantallaEnsenar, pantallaEnsenarSena } from './ui/ensenar.js';
import { pantallaAjustes } from './ui/ajustes.js';

const raiz = document.getElementById('app');
let limpiar = null, profundidad = 0;

const ir = (ruta) => { profundidad++; location.hash = '#/' + ruta; };
const volver = () => { if (profundidad > 0) { profundidad--; history.back(); } else location.hash = '#/'; };

function mostrar() {
  if (limpiar) { try { limpiar(); } catch { /* */ } limpiar = null; }
  const [nombre, parametro] = location.hash.replace(/^#\/?/, '').split('/');
  let arg = '';
  try { arg = parametro ? decodeURIComponent(parametro) : ''; } catch { arg = ''; }
  raiz.className = 'pantalla ' + (nombre || 'inicio');
  window.scrollTo(0, 0);
  ocultarAviso();
  document.querySelectorAll('body > .velo').forEach((n) => n.remove()); // ventanas abiertas de la pantalla anterior
  switch (nombre) {
    case 'conversacion': limpiar = pantallaConversacion(raiz, ir, volver); break;
    case 'emergencia': limpiar = pantallaEmergencia(raiz, ir, volver); break;
    case 'diccionario': limpiar = pantallaDiccionario(raiz, ir, volver, arg); break;
    case 'ensenar': limpiar = arg ? pantallaEnsenarSena(raiz, ir, volver, arg) : pantallaEnsenar(raiz, ir, volver); break;
    case 'ajustes': limpiar = pantallaAjustes(raiz, ir, volver); break;
    default: limpiar = pantallaInicio(raiz, ir);
  }
}

iniciarPWA();
poner(raiz, h('p', { class: 'cargando', text: 'Cargando…' }));
iniciar().then(() => { window.addEventListener('hashchange', mostrar); mostrar(); window.__senasLista = true; })
  .catch((e) => poner(raiz, h('p', { class: 'cargando', text: 'No se pudo iniciar la app: ' + (e && e.message ? e.message : e) })));
