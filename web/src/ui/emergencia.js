import { h, barra, boton, poner } from './dom.js';
import { app } from '../app/estado.js';
import { callar, decir, desbloquear } from '../app/voz.js';

/** Frases urgentes. No usa cámara, modelo ni conexión. */
export function pantallaEmergencia(raiz, ir, volver) {
  let direccion = 'sordo_a_oyente';
  const lista = h('div', { class: 'lista-emergencia' });
  const pestanas = h('div', { class: 'pestanas' });
  const cartel = h('div', { class: 'cartel', hidden: true });
  const hablar = (texto) => decir(texto, { velocidad: Math.min(1, app.ajustes.velocidadVoz), voz: app.ajustes.voz });

  function mostrar(f) {
    const habla = f.direccion === 'sordo_a_oyente';
    poner(cartel,
      h('p', { class: 'frase', text: f.espanol }),
      h('p', { class: 'pie', text: habla ? 'Muestre esta pantalla. El teléfono lo está diciendo en voz alta.' : 'Muestre esta pantalla a la persona sorda.' }),
      h('div', { class: 'fila' }, boton('Cerrar', () => { callar(); cartel.hidden = true; }, 'suave'), habla ? boton('Decir otra vez', () => hablar(f.espanol), 'peligro') : null));
    cartel.hidden = false;
    if (habla) hablar(f.espanol);
  }

  function pintar() {
    poner(pestanas,
      boton('Soy una persona sorda', () => { direccion = 'sordo_a_oyente'; pintar(); }, 'pestana' + (direccion === 'sordo_a_oyente' ? ' activa' : '')),
      boton('Atiendo a una persona sorda', () => { direccion = 'oyente_a_sordo'; pintar(); }, 'pestana' + (direccion === 'oyente_a_sordo' ? ' activa' : '')));
    poner(lista, app.emergencias.filter((f) => f.direccion === direccion).map((f) => boton(f.espanol, () => mostrar(f), direccion === 'sordo_a_oyente' ? 'peligro frase-emergencia' : 'suave frase-emergencia')));
  }

  poner(raiz, barra('Emergencia', volver), h('main', { class: 'pagina emergencia' }, pestanas, lista,
    h('p', { class: 'nota', text: 'Las frases se muestran y se dicen en español. En una emergencia médica o legal, pida un intérprete de LSRD.' })), cartel);
  raiz.querySelector('.barra').classList.add('roja');
  pintar();
  desbloquear();
  return () => callar();
}
