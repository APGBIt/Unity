using System;
using System.Collections.Generic;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Diccionario de la app: búsqueda por palabra y por tema, ficha de cada seña,
    /// estado de validación, referencia al Diccionario Oficial y animación del
    /// avatar cuando existe una grabación aprobada.
    /// </summary>
    public sealed class PantallaDiccionario
    {
        public const int MaximoVisible = 60;

        public readonly RectTransform Raiz;
        /// <summary>Lienzo donde el avatar muestra la seña de la ficha abierta.</summary>
        public readonly GraficoEsqueleto Vista;
        public event Action AlVolver;
        public event Action<FichaSena> AlVerSena;
        public event Action AlCerrarFicha;
        public event Action<string> AlAbrirReferencia;
        public event Action<FichaSena> AlReportarSena;

        readonly DiccionarioLocal diccionario;
        readonly Func<string, bool> tieneAnimacion;
        readonly RectTransform lista, ficha;
        readonly Text conteo, fichaTitulo, fichaDatos, fichaEstado, fichaAnimacion;
        readonly Button botonVer, botonReferencia, botonReportar;
        readonly InputField campo;
        string categoria;
        FichaSena abierta;

        public PantallaDiccionario(Transform padre, DiccionarioLocal dic, Func<string, bool> tieneAnimacion)
        {
            diccionario = dic;
            this.tieneAnimacion = tieneAnimacion ?? (g => false);
            Raiz = UIFabrica.Nodo(padre, "PantallaDiccionario");
            UIFabrica.Estirar(Raiz);
            Raiz.gameObject.AddComponent<Image>().color = Tema.Fondo;
            var col = UIFabrica.Nodo(Raiz, "Columna");
            UIFabrica.Anclar(col, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(col.gameObject, 18);

            var barra = UIFabrica.Nodo(col, "Barra");
            UIFabrica.Horizontal(barra.gameObject, 20, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(barra, 110);
            UIFabrica.Elemento(UIFabrica.Boton(barra, "‹ Menú", () => AlVolver?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0), 100, 200);
            UIFabrica.Elemento(UIFabrica.Texto(barra, "Diccionario", Tema.Titulo, Tema.Acento, TextAnchor.MiddleLeft, FontStyle.Bold), 100, -1, 1);

            campo = UIFabrica.Campo(col, "Buscar una palabra…", "", null);
            campo.onValueChanged.AddListener(v => Llenar());

            var opciones = new List<string> { "Todos los temas" };
            foreach (var c in dic.Categorias) opciones.Add(NombreCategoria(c));
            UIFabrica.Desplegable(col, opciones, 0, i => { categoria = i == 0 ? null : dic.Categorias[i - 1]; Llenar(); });

            conteo = UIFabrica.Texto(col, "", Tema.Mini, Tema.TextoSecundario, TextAnchor.MiddleLeft);
            UIFabrica.Elemento(conteo, 44);

            var zona = UIFabrica.Nodo(col, "Zona");
            UIFabrica.Elemento(zona, -1, -1, -1, 1);
            lista = UIFabrica.Desplazable(zona, "Lista", 10, 4);
            UIFabrica.Estirar((RectTransform)lista.parent);

            // ------------------------------------------------ ficha de una seña
            ficha = UIFabrica.Panel(Raiz, "Ficha", Tema.Fondo, false).rectTransform;
            UIFabrica.Estirar(ficha);
            var colF = UIFabrica.Nodo(ficha, "Columna");
            UIFabrica.Anclar(colF, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(colF.gameObject, 16);
            UIFabrica.Elemento(UIFabrica.Boton(colF, "‹ Volver a la lista", CerrarFicha, Tema.SuperficieClara, null, Tema.Pequeno, 0), 100);
            fichaTitulo = UIFabrica.Texto(colF, "", Tema.Titulo, Tema.Texto, TextAnchor.MiddleLeft, FontStyle.Bold);
            fichaTitulo.resizeTextForBestFit = true;
            fichaTitulo.resizeTextMinSize = 30;
            fichaTitulo.resizeTextMaxSize = Tema.Titulo;
            UIFabrica.Elemento(fichaTitulo, 100);
            fichaDatos = UIFabrica.Texto(colF, "", Tema.Pequeno, Tema.TextoSecundario, TextAnchor.UpperLeft);
            UIFabrica.Elemento(fichaDatos, 150);
            fichaEstado = UIFabrica.Texto(colF, "", Tema.Pequeno, Tema.Advertencia, TextAnchor.UpperLeft);
            UIFabrica.Elemento(fichaEstado, 110);

            var marco = UIFabrica.Panel(colF, "Avatar", Tema.Superficie);
            UIFabrica.Elemento(marco, -1, -1, -1, 1);
            var lienzo = UIFabrica.Nodo(marco.transform, "Lienzo");
            UIFabrica.Estirar(lienzo, 16, 16, 16, 16);
            Vista = lienzo.gameObject.AddComponent<GraficoEsqueleto>();
            Vista.ModoAvatar = true;
            fichaAnimacion = UIFabrica.Texto(marco.transform, "", Tema.Pequeno, Tema.TextoSecundario, TextAnchor.MiddleCenter);
            UIFabrica.Estirar(fichaAnimacion.rectTransform, 32, 32, 32, 32);

            var acciones = UIFabrica.Nodo(colF, "Acciones");
            UIFabrica.Horizontal(acciones.gameObject, 14);
            UIFabrica.Elemento(acciones, 120);
            botonVer = UIFabrica.Boton(acciones, "Ver la seña", () => { if (abierta != null) AlVerSena?.Invoke(abierta); },
                Tema.Acento, Tema.Fondo, Tema.Pequeno, 0);
            botonReferencia = UIFabrica.Boton(acciones, "Diccionario Oficial",
                () => { if (abierta != null && abierta.UrlOficial.Length > 0) AlAbrirReferencia?.Invoke(abierta.UrlOficial); },
                Tema.SuperficieClara, null, Tema.Pequeno, 0);
            botonReportar = UIFabrica.Boton(acciones, "Reportar", () => { if (abierta != null) AlReportarSena?.Invoke(abierta); },
                Tema.SuperficieClara, null, Tema.Pequeno, 0);
            ficha.gameObject.SetActive(false);
            Llenar();
        }

        public static string NombreCategoria(string c)
        {
            switch (c)
            {
                case "tramites": return "Trámites";
                case "comunicacion": return "Comunicación";
                case "cortesia": return "Cortesía";
                case "dactilologico": return "Alfabeto manual";
                case "numeros": return "Números";
                default: return string.IsNullOrEmpty(c) ? "" : char.ToUpperInvariant(c[0]) + c.Substring(1);
            }
        }

        /// <summary>Vuelve a la lista (al entrar a la pantalla o cuando cambia la biblioteca del avatar).</summary>
        public void Reiniciar()
        {
            ficha.gameObject.SetActive(false);
            abierta = null;
            Llenar();
        }

        void Llenar()
        {
            UIFabrica.Limpiar(lista);
            var resultados = diccionario.Buscar(campo != null ? campo.text : "", categoria);
            int visibles = Mathf.Min(resultados.Count, MaximoVisible);
            conteo.text = resultados.Count == 0
                ? "No está en el vocabulario de la app. Puede deletrearla con el alfabeto manual."
                : visibles < resultados.Count
                    ? $"Mostrando {visibles} de {resultados.Count}. Escriba para acotar."
                    : $"{resultados.Count} señas";
            for (int i = 0; i < visibles; i++)
            {
                var f = resultados[i];
                string marca = tieneAnimacion(f.Entrada.Glosa) ? "  ▶" : "";
                var b = UIFabrica.Boton(lista, f.Entrada.Espanol + marca, () => Abrir(f), Tema.SuperficieClara, null, Tema.Normal, 100);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.fontStyle = FontStyle.Normal;
            }
        }

        void Abrir(FichaSena f)
        {
            abierta = f;
            var e = f.Entrada;
            fichaTitulo.text = e.Espanol;
            string otras = e.Sinonimos != null && e.Sinonimos.Length > 0 ? "\nTambién: " + string.Join(", ", e.Sinonimos) : "";
            fichaDatos.text = $"Glosa: {e.Glosa}   ·   Tema: {NombreCategoria(e.Categoria)}{otras}";
            fichaEstado.color = f.Validada ? Tema.Exito : Tema.Advertencia;
            fichaEstado.text = (f.Validada
                    ? "Validada por la comunidad sorda."
                    : "Propuesta: falta validarla con personas sordas e intérpretes.")
                + (f.TieneReferenciaOficial
                    ? $"\nEn el Diccionario Oficial: «{f.TituloOficial}»."
                    : "\nNo se localizó en el índice del Diccionario Oficial.");
            bool hay = tieneAnimacion(e.Glosa);
            botonVer.interactable = hay;
            botonReferencia.interactable = f.UrlOficial.Length > 0;
            fichaAnimacion.text = hay ? "" : "Todavía no hay una grabación aprobada de esta seña.";
            Vista.Limpiar();
            ficha.gameObject.SetActive(true);
            if (hay) AlVerSena?.Invoke(f);
        }

        void CerrarFicha()
        {
            ficha.gameObject.SetActive(false);
            abierta = null;
            AlCerrarFicha?.Invoke();
        }

        /// <summary>Oculta el aviso de «sin grabación» mientras el avatar reproduce.</summary>
        public void AvisoAnimacion(string texto) => fichaAnimacion.text = texto ?? "";
    }
}
