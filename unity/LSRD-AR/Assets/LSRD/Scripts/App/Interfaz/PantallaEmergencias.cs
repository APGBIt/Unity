using System;
using System.Collections.Generic;
using System.Linq;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Acceso rápido para emergencias. Funciona sin servidor, sin cámara y sin
    /// modelo: la persona sorda toca una frase y el teléfono la muestra en grande
    /// y la dice en voz alta; quien atiende puede mostrarle una frase escrita.
    /// </summary>
    public sealed class PantallaEmergencias
    {
        public readonly RectTransform Raiz;
        public event Action AlVolver;
        /// <summary>La persona sorda eligió una frase: hay que decirla en voz alta.</summary>
        public event Action<FraseRapida> AlDecir;

        readonly RectTransform lista, cartel;
        readonly Text textoCartel, pieCartel;
        readonly Button botonSordo, botonOyente, botonRepetir;
        List<FraseRapida> frases = new List<FraseRapida>();
        string direccion = "sordo_a_oyente";
        FraseRapida actual;

        public PantallaEmergencias(Transform padre)
        {
            Raiz = UIFabrica.Nodo(padre, "PantallaEmergencias");
            UIFabrica.Estirar(Raiz);
            Raiz.gameObject.AddComponent<Image>().color = Tema.Fondo;
            var col = UIFabrica.Nodo(Raiz, "Columna");
            UIFabrica.Anclar(col, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(col.gameObject, 20);

            var barra = UIFabrica.Nodo(col, "Barra");
            UIFabrica.Horizontal(barra.gameObject, 20, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(barra, 110);
            UIFabrica.Elemento(UIFabrica.Boton(barra, "‹ Volver", () => AlVolver?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0), 100, 230);
            UIFabrica.Elemento(UIFabrica.Texto(barra, "EMERGENCIA", Tema.Titulo, Tema.Peligro, TextAnchor.MiddleLeft, FontStyle.Bold), 100, -1, 1);

            var pestanas = UIFabrica.Nodo(col, "Pestanas");
            UIFabrica.Horizontal(pestanas.gameObject, 16);
            UIFabrica.Elemento(pestanas, 120);
            botonSordo = UIFabrica.Boton(pestanas, "Soy una persona sorda", () => Elegir("sordo_a_oyente"), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            botonOyente = UIFabrica.Boton(pestanas, "Atiendo a una persona sorda", () => Elegir("oyente_a_sordo"), Tema.SuperficieClara, null, Tema.Pequeno, 0);

            var zona = UIFabrica.Nodo(col, "Zona");
            UIFabrica.Elemento(zona, -1, -1, -1, 1);
            lista = UIFabrica.Desplazable(zona, "Lista", 16, 4);
            UIFabrica.Estirar((RectTransform)lista.parent);

            var nota = UIFabrica.Texto(col,
                "Estas frases se muestran y se dicen en español. En una emergencia médica o legal, pida un intérprete de LSRD.",
                Tema.Mini, Tema.Advertencia, TextAnchor.UpperLeft);
            UIFabrica.Elemento(nota, 90);

            // Cartel a pantalla completa con la frase elegida.
            cartel = UIFabrica.Panel(Raiz, "Cartel", Tema.Fondo, false).rectTransform;
            UIFabrica.Estirar(cartel);
            var colCartel = UIFabrica.Nodo(cartel, "Columna");
            UIFabrica.Anclar(colCartel, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(colCartel.gameObject, 24);
            textoCartel = UIFabrica.Texto(colCartel, "", 120, Tema.Texto, TextAnchor.MiddleCenter, FontStyle.Bold);
            textoCartel.resizeTextForBestFit = true;
            textoCartel.resizeTextMinSize = 40;
            textoCartel.resizeTextMaxSize = 160;
            UIFabrica.Elemento(textoCartel, -1, -1, -1, 1);
            pieCartel = UIFabrica.Texto(colCartel, "", Tema.Pequeno, Tema.TextoSecundario, TextAnchor.MiddleCenter);
            UIFabrica.Elemento(pieCartel, 80);
            var acciones = UIFabrica.Nodo(colCartel, "Acciones");
            UIFabrica.Horizontal(acciones.gameObject, 16);
            UIFabrica.Elemento(acciones, 150);
            UIFabrica.Boton(acciones, "Cerrar", () => cartel.gameObject.SetActive(false), Tema.SuperficieClara, null, Tema.Normal, 0);
            botonRepetir = UIFabrica.Boton(acciones, "Decir otra vez", () => { if (actual != null) AlDecir?.Invoke(actual); },
                Tema.Peligro, Tema.Texto, Tema.Normal, 0);
            cartel.gameObject.SetActive(false);
        }

        public void Cargar(List<FraseRapida> lista)
        {
            frases = lista ?? new List<FraseRapida>();
            Elegir(direccion);
        }

        /// <summary>Vuelve a la lista (al entrar a la pantalla).</summary>
        public void Reiniciar()
        {
            cartel.gameObject.SetActive(false);
            Elegir("sordo_a_oyente");
        }

        void Elegir(string d)
        {
            direccion = d;
            botonSordo.image.color = d == "sordo_a_oyente" ? Tema.AcentoOscuro : Tema.SuperficieClara;
            botonOyente.image.color = d == "oyente_a_sordo" ? Tema.AcentoOscuro : Tema.SuperficieClara;
            UIFabrica.Limpiar(lista);
            foreach (var f in frases.Where(f => f.Direccion == d))
            {
                var frase = f;
                var b = UIFabrica.Boton(lista, f.Espanol, () => Mostrar(frase),
                    d == "sordo_a_oyente" ? Tema.Peligro : Tema.SuperficieClara, Tema.Texto, Tema.Grande, 170);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            }
        }

        void Mostrar(FraseRapida f)
        {
            actual = f;
            bool hablar = f.Direccion == "sordo_a_oyente";
            textoCartel.text = f.Espanol;
            pieCartel.text = hablar ? "Muestre esta pantalla. El teléfono lo está diciendo en voz alta."
                                    : "Muestre esta pantalla a la persona sorda.";
            botonRepetir.gameObject.SetActive(hablar);
            cartel.gameObject.SetActive(true);
            if (hablar) AlDecir?.Invoke(f);
        }
    }
}
