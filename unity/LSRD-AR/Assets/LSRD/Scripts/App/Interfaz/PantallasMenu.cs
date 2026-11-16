using System;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>Menú principal con el estado del sistema.</summary>
    public sealed class PantallaInicio
    {
        public readonly RectTransform Raiz;
        public event Action AlConversacion;
        public event Action AlRecoleccion;
        public event Action AlAjustes;
        public event Action AlDiccionario;
        public event Action AlEmergencia;
        readonly Text estado;

        public PantallaInicio(Transform padre)
        {
            Raiz = UIFabrica.Nodo(padre, "PantallaInicio");
            UIFabrica.Estirar(Raiz);
            Raiz.gameObject.AddComponent<Image>().color = Tema.Fondo;
            var col = UIFabrica.Nodo(Raiz, "Columna");
            UIFabrica.Anclar(col, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(col.gameObject, 28);

            UIFabrica.Elemento(UIFabrica.Texto(col, "LSRD", 120, Tema.Acento, TextAnchor.LowerLeft, FontStyle.Bold), 180);
            UIFabrica.Elemento(UIFabrica.Texto(col, "Traductor de Lengua de Señas Dominicana", Tema.Grande, Tema.Texto, TextAnchor.UpperLeft, FontStyle.Bold), 130);
            UIFabrica.Elemento(UIFabrica.Texto(col, "Prototipo para atención al público", Tema.Normal, Tema.TextoSecundario, TextAnchor.UpperLeft), 70);
            UIFabrica.Elemento(UIFabrica.Nodo(col, "Espacio"), 40);
            UIFabrica.Boton(col, "Conversación", () => AlConversacion?.Invoke(), Tema.Acento, Tema.Fondo, Tema.Grande, 170);
            UIFabrica.Boton(col, "Emergencia", () => AlEmergencia?.Invoke(), Tema.Peligro, Tema.Texto, Tema.Grande, 150);
            UIFabrica.Boton(col, "Diccionario", () => AlDiccionario?.Invoke(), Tema.SuperficieClara, null, Tema.Normal, 130);
            var filaMenu = UIFabrica.Nodo(col, "Secundarios");
            UIFabrica.Horizontal(filaMenu.gameObject, 16);
            UIFabrica.Elemento(filaMenu, 120);
            UIFabrica.Boton(filaMenu, "Recolección de datos", () => AlRecoleccion?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(filaMenu, "Ajustes", () => AlAjustes?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);

            var caja = UIFabrica.Panel(col, "Estado", Tema.Superficie);
            UIFabrica.Elemento(caja, 460);
            estado = UIFabrica.Texto(caja.transform, "", Tema.Pequeno, Tema.TextoSecundario, TextAnchor.UpperLeft);
            UIFabrica.Estirar(estado.rectTransform, 28, 24, 28, 24);

            var nota = UIFabrica.Texto(col,
                "Esta app apoya la comunicación cotidiana; no reemplaza a un intérprete de LSRD. " +
                "En trámites legales, médicos o judiciales, solicite siempre un intérprete.",
                Tema.Mini, Tema.Advertencia, TextAnchor.UpperLeft);
            UIFabrica.Elemento(nota, 120);
        }

        public void MostrarEstado(string texto) => estado.text = texto;
    }

    /// <summary>Ajustes del servidor, la cámara, el reconocimiento y la voz.</summary>
    public sealed class PantallaAjustes
    {
        public readonly RectTransform Raiz;
        public event Action AlGuardar;
        public event Action AlProbarConexion;
        public event Action AlSincronizarAvatar;
        public event Action AlVolver;
        readonly AjustesApp a;
        readonly Text resultado;

        static readonly string[] Rotaciones = { "Automática", "0°", "90°", "180°", "270°" };

        public PantallaAjustes(Transform padre, AjustesApp ajustes)
        {
            a = ajustes;
            Raiz = UIFabrica.Nodo(padre, "PantallaAjustes");
            UIFabrica.Estirar(Raiz);
            Raiz.gameObject.AddComponent<Image>().color = Tema.Fondo;
            var lista = UIFabrica.Desplazable(Raiz, "Lista", 16, 40);
            UIFabrica.Estirar((RectTransform)lista.parent);

            Seccion(lista, "Servidor");
            Etiqueta(lista, "Dirección (ej. http://192.168.1.10:8000)");
            UIFabrica.Campo(lista, "http://…", a.UrlServidor, v => a.UrlServidor = v.Trim(), InputField.ContentType.Standard);
            Etiqueta(lista, "Clave (cabecera X-Clave-LSRD)");
            UIFabrica.Campo(lista, "opcional", a.Clave, v => a.Clave = v, InputField.ContentType.Password);
            var filaS = UIFabrica.Nodo(lista, "Acciones");
            UIFabrica.Horizontal(filaS.gameObject, 16);
            UIFabrica.Elemento(filaS, 110);
            UIFabrica.Boton(filaS, "Probar conexión", () => AlProbarConexion?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(filaS, "Descargar avatar", () => AlSincronizarAvatar?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            resultado = UIFabrica.Texto(lista, "", Tema.Pequeno, Tema.TextoSecundario);
            UIFabrica.Elemento(resultado, 120);

            Seccion(lista, "Cámara");
            UIFabrica.Interruptor(lista, "Usar la cámara frontal", a.CamaraFrontal, v => a.CamaraFrontal = v);
            UIFabrica.Interruptor(lista, "Mostrar en espejo", a.EspejoPantalla, v => a.EspejoPantalla = v);
            UIFabrica.Interruptor(lista, "Dibujar los puntos clave", a.MostrarEsqueleto, v => a.MostrarEsqueleto = v);
            Etiqueta(lista, "Rotación de la imagen");
            int rot = a.RotacionForzada < 0 ? 0 : 1 + (a.RotacionForzada / 90) % 4;
            UIFabrica.Desplegable(lista, Rotaciones, rot, i => a.RotacionForzada = i == 0 ? -1 : (i - 1) * 90);
            UIFabrica.Interruptor(lista, "Detector en GPU (solo Android)", a.UsarGpuMediaPipe, v => a.UsarGpuMediaPipe = v);
            UIFabrica.Interruptor(lista, "Usar detector simulado (pruebas sin cámara)", a.DetectorSimulado, v => a.DetectorSimulado = v);

            Seccion(lista, "Reconocimiento");
            Deslizador(lista, "Confianza para aceptar sin dudas", 0.3f, 0.95f, a.UmbralConfianza, v => a.UmbralConfianza = v, "P0");
            Deslizador(lista, "Confianza mínima (debajo se ignora)", 0.1f, 0.7f, a.UmbralMinimo, v => a.UmbralMinimo = v, "P0");
            Deslizador(lista, "Pausa que cierra la frase (segundos)", 0.8f, 4f, a.PausaFraseS, v => a.PausaFraseS = v, "0.0");

            Seccion(lista, "Voz y avatar");
            UIFabrica.Interruptor(lista, "Decir en voz alta la traducción", a.HablarTraduccion, v => a.HablarTraduccion = v);
            UIFabrica.Interruptor(lista, "Vibrar al traducir", a.Vibrar, v => a.Vibrar = v);
            Deslizador(lista, "Sensibilidad de la escucha automática", 0.005f, 0.08f, a.UmbralVoz, v => a.UmbralVoz = v, "0.000");
            Deslizador(lista, "Velocidad del avatar", 0.5f, 1.5f, a.VelocidadAvatar, v => a.VelocidadAvatar = v, "0.00");

            var fila = UIFabrica.Nodo(lista, "Guardar");
            UIFabrica.Horizontal(fila.gameObject, 16);
            UIFabrica.Elemento(fila, 130);
            UIFabrica.Boton(fila, "Volver", () => AlVolver?.Invoke(), Tema.SuperficieClara, null, Tema.Normal, 0);
            UIFabrica.Boton(fila, "Guardar", () => AlGuardar?.Invoke(), Tema.Acento, Tema.Fondo, Tema.Normal, 0);
        }

        static void Seccion(Transform p, string t) =>
            UIFabrica.Elemento(UIFabrica.Texto(p, t, Tema.Grande, Tema.Acento, TextAnchor.LowerLeft, FontStyle.Bold), 110);

        static void Etiqueta(Transform p, string t) =>
            UIFabrica.Elemento(UIFabrica.Texto(p, t, Tema.Pequeno, Tema.TextoSecundario, TextAnchor.LowerLeft), 50);

        static void Deslizador(Transform p, string etiqueta, float min, float max, float valor, Action<float> cambio, string formato)
        {
            var t = UIFabrica.Texto(p, "", Tema.Pequeno, Tema.TextoSecundario, TextAnchor.LowerLeft);
            UIFabrica.Elemento(t, 50);
            void Rotular(float v) => t.text = $"{etiqueta}: {v.ToString(formato, System.Globalization.CultureInfo.InvariantCulture)}";
            Rotular(valor);
            UIFabrica.Deslizador(p, min, max, valor, v => { cambio(v); Rotular(v); });
        }

        public void Resultado(string texto) => resultado.text = texto;
    }
}
