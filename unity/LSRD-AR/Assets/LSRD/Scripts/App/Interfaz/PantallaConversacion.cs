using System;
using System.Collections.Generic;
using System.Linq;
using LSRD.Nucleo;
using UnityEngine;
using UnityEngine.UI;

namespace LSRD.App
{
    /// <summary>
    /// Pantalla principal de atención al público. El teléfono o la tableta mira a la persona sorda:
    /// ve el avatar con lo que dice el empleado y, junto a su cabeza, el texto de lo que firmó.
    /// El empleado escucha la traducción en voz alta y habla al micrófono.
    /// </summary>
    public sealed class PantallaConversacion
    {
        public readonly RectTransform Raiz;
        public readonly RawImage VistaCamara;
        public readonly GraficoEsqueleto Esqueleto;
        public readonly GraficoEsqueleto Avatar;
        public readonly RawImage Avatar3D;
        public readonly Text RotuloAvatar;
        public readonly Text TarjetaAvatar;

        public event Action AlVolver;
        public event Action AlEnviar;
        public event Action AlBorrar;
        public event Action AlRepetirAvatar;
        public event Action<bool> AlDeletreo;
        public event Action<bool> AlEscuchaAutomatica;
        public event Action AlEmpezarHablar;
        public event Action AlTerminarHablar;
        public event Action<string> AlEscribir;
        public event Action<FraseRapida> AlFraseRapida;
        public event Action<int, string> AlCorregir;
        public event Action<float> AlVelocidadAvatar;

        readonly AspectRatioFitter ajusteCamara;
        readonly Image indicadorManos, indicadorServidor;
        readonly Text textoManos, textoServidor, textoModo, aviso;
        readonly RectTransform burbuja;
        readonly Text textoBurbuja;
        readonly Text textoPrincipal, textoGlosas, etiquetaConfianza;
        readonly RectTransform barraConfianza;
        readonly Image rellenoConfianza;
        readonly RectTransform chips;
        readonly Text textoEmpleado, fuenteEmpleado;
        readonly Button botonHablar;
        readonly Toggle escucha, deletreo;
        readonly RectTransform modalEscribir, modalFrases, listaFrases;
        readonly InputField campoEscribir;
        readonly Text nivelMicrofono;
        List<FraseRapida> frases = new List<FraseRapida>();
        string direccionFrases = "oyente_a_sordo";
        Vector3 posicionBurbuja;
        float finBurbuja;
        bool espejo;

        public PantallaConversacion(Transform padre)
        {
            Raiz = UIFabrica.Nodo(padre, "PantallaConversacion");
            UIFabrica.Estirar(Raiz);
            var fondo = Raiz.gameObject.AddComponent<Image>();
            fondo.color = Color.black;

            // ------------------------------------------------ cámara + esqueleto
            var marco = UIFabrica.Nodo(Raiz, "MarcoCamara");
            UIFabrica.Estirar(marco);
            marco.gameObject.AddComponent<RectMask2D>();
            var cam = UIFabrica.Nodo(marco, "Camara");
            VistaCamara = cam.gameObject.AddComponent<RawImage>();
            VistaCamara.color = Tema.Fondo;
            VistaCamara.raycastTarget = false;
            ajusteCamara = cam.gameObject.AddComponent<AspectRatioFitter>();
            ajusteCamara.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            ajusteCamara.aspectRatio = 9f / 16f;
            var esq = UIFabrica.Nodo(cam, "Esqueleto");
            UIFabrica.Estirar(esq);
            Esqueleto = esq.gameObject.AddComponent<GraficoEsqueleto>();

            // ------------------------------------------------ barra superior
            var barra = UIFabrica.Panel(Raiz, "BarraSuperior", Tema.ConAlfa(Tema.Fondo, 0.82f), false).rectTransform;
            UIFabrica.Anclar(barra, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 130));
            UIFabrica.Horizontal(barra.gameObject, 18, 16, false).childAlignment = TextAnchor.MiddleLeft;
            var volver = UIFabrica.Boton(barra, "‹ Menú", () => AlVolver?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Elemento(volver, 96, 200);
            indicadorManos = Indicador(barra, "Manos", out textoManos);
            indicadorServidor = Indicador(barra, "Servidor", out textoServidor);
            textoModo = UIFabrica.Texto(barra, "", Tema.Mini, Tema.Advertencia);
            UIFabrica.Elemento(textoModo, 96, -1, 1);
            deletreo = UIFabrica.Interruptor(barra, "Deletreo", false, v => AlDeletreo?.Invoke(v), Tema.Pequeno);
            UIFabrica.Elemento(deletreo, 96, 290);

            // ------------------------------------------------ aviso (errores, modelo de demostración)
            aviso = UIFabrica.Texto(Raiz, "", Tema.Mini, Tema.Advertencia, TextAnchor.UpperLeft);
            UIFabrica.Anclar(aviso.rectTransform, new Vector2(0, 1), new Vector2(0.56f, 1), new Vector2(0, 1), new Vector2(24, -146), new Vector2(0, 120));

            // ------------------------------------------------ panel del avatar (lo que dice el empleado)
            var panelAvatar = UIFabrica.Panel(Raiz, "PanelAvatar", Tema.Superficie).rectTransform;
            UIFabrica.Anclar(panelAvatar, new Vector2(0.56f, 0.56f), new Vector2(1f, 1f), new Vector2(1, 1), new Vector2(-20, -150), Vector2.zero);
            panelAvatar.offsetMin = new Vector2(0, 0);
            var zonaAvatar = UIFabrica.Nodo(panelAvatar, "ZonaAvatar");
            UIFabrica.Estirar(zonaAvatar, 12, 220, 12, 12);
            var av3d = UIFabrica.Nodo(zonaAvatar, "Avatar3D");
            UIFabrica.Estirar(av3d);
            Avatar3D = av3d.gameObject.AddComponent<RawImage>();
            Avatar3D.enabled = false;
            var av = UIFabrica.Nodo(zonaAvatar, "AvatarEsquematico");
            UIFabrica.Estirar(av);
            Avatar = av.gameObject.AddComponent<GraficoEsqueleto>();
            Avatar.ModoAvatar = true;
            TarjetaAvatar = UIFabrica.Texto(zonaAvatar, "", Tema.Grande, Tema.Texto, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFabrica.Estirar(TarjetaAvatar.rectTransform, 12, 12, 12, 12);
            TarjetaAvatar.resizeTextForBestFit = true;
            TarjetaAvatar.resizeTextMaxSize = Tema.Titulo;
            RotuloAvatar = UIFabrica.Texto(panelAvatar, "", Tema.Pequeno, Tema.Acento, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFabrica.Anclar(RotuloAvatar.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(-24, 80));
            var controlesAvatar = UIFabrica.Nodo(panelAvatar, "Controles");
            UIFabrica.Anclar(controlesAvatar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(-24, 104));
            UIFabrica.Horizontal(controlesAvatar.gameObject, 12);
            UIFabrica.Boton(controlesAvatar, "Repetir", () => AlRepetirAvatar?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(controlesAvatar, "Lento", () => AlVelocidadAvatar?.Invoke(0.7f), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(controlesAvatar, "Normal", () => AlVelocidadAvatar?.Invoke(1f), Tema.SuperficieClara, null, Tema.Pequeno, 0);

            var panelEmpleado = UIFabrica.Panel(Raiz, "PanelEmpleado", Tema.Superficie).rectTransform;
            UIFabrica.Anclar(panelEmpleado, new Vector2(0.56f, 0.43f), new Vector2(1f, 0.555f), new Vector2(1, 1), new Vector2(-20, 0), Vector2.zero);
            panelEmpleado.offsetMin = new Vector2(0, 0);
            panelEmpleado.offsetMax = new Vector2(-20, -8);
            fuenteEmpleado = UIFabrica.Texto(panelEmpleado, "EMPLEADO", Tema.Mini, Tema.TextoSecundario, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFabrica.Anclar(fuenteEmpleado.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(-40, 40));
            textoEmpleado = UIFabrica.Texto(panelEmpleado, "", Tema.Pequeno, Tema.Texto, TextAnchor.UpperLeft);
            UIFabrica.Estirar(textoEmpleado.rectTransform, 20, 12, 20, 56);
            textoEmpleado.resizeTextForBestFit = true;
            textoEmpleado.resizeTextMinSize = 18;
            textoEmpleado.resizeTextMaxSize = Tema.Pequeno;

            // ------------------------------------------------ burbuja AR junto a la persona
            var b = UIFabrica.Panel(Raiz, "Burbuja", Tema.ConAlfa(Tema.Fondo, 0.9f));
            burbuja = b.rectTransform;
            UIFabrica.Anclar(burbuja, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(620, 150));
            textoBurbuja = UIFabrica.Texto(burbuja, "", Tema.Normal, Tema.Texto, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFabrica.Estirar(textoBurbuja.rectTransform, 20, 10, 20, 10);
            textoBurbuja.resizeTextForBestFit = true;
            textoBurbuja.resizeTextMinSize = 20;
            textoBurbuja.resizeTextMaxSize = Tema.Normal;
            burbuja.gameObject.SetActive(false);

            // ------------------------------------------------ hoja inferior (lo que firma la persona sorda)
            var hoja = UIFabrica.Panel(Raiz, "HojaInferior", Tema.Superficie).rectTransform;
            UIFabrica.Anclar(hoja, new Vector2(0, 0), new Vector2(1, 0.36f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            hoja.offsetMin = new Vector2(16, 16);
            hoja.offsetMax = new Vector2(-16, 0);
            UIFabrica.Vertical(hoja.gameObject, 12, 24);

            var filaConfianza = UIFabrica.Nodo(hoja, "FilaConfianza");
            UIFabrica.Horizontal(filaConfianza.gameObject, 16, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(filaConfianza, 44);
            var etiqueta = UIFabrica.Texto(filaConfianza, "PERSONA SORDA", Tema.Mini, Tema.TextoSecundario, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFabrica.Elemento(etiqueta, 44, 250);
            var barraC = UIFabrica.Panel(filaConfianza, "Confianza", Tema.SuperficieClara);
            UIFabrica.Elemento(barraC, 18, -1, 1);
            barraConfianza = barraC.rectTransform;
            rellenoConfianza = UIFabrica.Panel(barraC.transform, "Relleno", Tema.Acento);
            UIFabrica.Estirar(rellenoConfianza.rectTransform);
            etiquetaConfianza = UIFabrica.Texto(filaConfianza, "", Tema.Mini, Tema.TextoSecundario, TextAnchor.MiddleRight);
            UIFabrica.Elemento(etiquetaConfianza, 44, 220);

            textoPrincipal = UIFabrica.Texto(hoja, "Firme frente a la cámara. Haga una pausa al terminar la frase.", Tema.Grande, Tema.Texto,
                TextAnchor.UpperLeft, FontStyle.Bold);
            textoPrincipal.resizeTextForBestFit = true;
            textoPrincipal.resizeTextMinSize = 26;
            textoPrincipal.resizeTextMaxSize = Tema.Grande;
            UIFabrica.Elemento(textoPrincipal, -1, -1, -1, 1);
            textoGlosas = UIFabrica.Texto(hoja, "", Tema.Pequeno, Tema.TextoSecundario);
            UIFabrica.Elemento(textoGlosas, 44);

            chips = UIFabrica.Nodo(hoja, "Alternativas");
            UIFabrica.Horizontal(chips.gameObject, 12, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(chips, 84);

            var botones = UIFabrica.Nodo(hoja, "Botones");
            UIFabrica.Horizontal(botones.gameObject, 12);
            UIFabrica.Elemento(botones, 124);
            botonHablar = UIFabrica.Boton(botones, "Mantener para hablar", null, Tema.Acento, Tema.Fondo, Tema.Pequeno, 0);
            UIFabrica.Elemento(botonHablar, 124, 330, 2);
            var mantener = botonHablar.gameObject.AddComponent<BotonMantener>();
            mantener.AlPresionar = () => AlEmpezarHablar?.Invoke();
            mantener.AlSoltar = () => AlTerminarHablar?.Invoke();
            UIFabrica.Boton(botones, "Escribir", () => MostrarEscribir(true), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(botones, "Frases", () => MostrarFrases(true), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(botones, "Enviar", () => AlEnviar?.Invoke(), Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(botones, "Borrar", () => AlBorrar?.Invoke(), Tema.SuperficieClara, Tema.Peligro, Tema.Pequeno, 0);

            var filaEscucha = UIFabrica.Nodo(hoja, "FilaEscucha");
            UIFabrica.Horizontal(filaEscucha.gameObject, 16, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(filaEscucha, 80);
            escucha = UIFabrica.Interruptor(filaEscucha, "Escucha automática del empleado", false, v => AlEscuchaAutomatica?.Invoke(v), Tema.Pequeno);
            UIFabrica.Elemento(escucha, 80, -1, 1);
            nivelMicrofono = UIFabrica.Texto(filaEscucha, "", Tema.Mini, Tema.TextoSecundario, TextAnchor.MiddleRight);
            UIFabrica.Elemento(nivelMicrofono, 80, 260);

            // ------------------------------------------------ ventanas
            modalEscribir = Modal("EscribirMensaje", out var cuerpoEscribir);
            UIFabrica.Texto(cuerpoEscribir, "Mensaje del empleado para el avatar", Tema.Normal, Tema.Texto, TextAnchor.MiddleLeft, FontStyle.Bold);
            campoEscribir = UIFabrica.Campo(cuerpoEscribir, "Escriba aquí…", "", null, InputField.ContentType.Standard, Tema.Normal, 220);
            campoEscribir.lineType = InputField.LineType.MultiLineNewline;
            var filaE = UIFabrica.Nodo(cuerpoEscribir, "Acciones");
            UIFabrica.Horizontal(filaE.gameObject, 16);
            UIFabrica.Elemento(filaE, 110);
            UIFabrica.Boton(filaE, "Cancelar", () => MostrarEscribir(false), Tema.SuperficieClara, null, Tema.Normal, 0);
            UIFabrica.Boton(filaE, "Firmar", () =>
            {
                var t = campoEscribir.text.Trim();
                MostrarEscribir(false);
                if (t.Length > 0) AlEscribir?.Invoke(t);
                campoEscribir.text = "";
            }, Tema.Acento, Tema.Fondo, Tema.Normal, 0);

            modalFrases = Modal("FrasesRapidas", out var cuerpoFrases);
            var pestanas = UIFabrica.Nodo(cuerpoFrases, "Pestanas");
            UIFabrica.Horizontal(pestanas.gameObject, 12);
            UIFabrica.Elemento(pestanas, 104);
            UIFabrica.Boton(pestanas, "Dice el empleado", () => { direccionFrases = "oyente_a_sordo"; LlenarFrases(); }, Tema.SuperficieClara, null, Tema.Pequeno, 0);
            UIFabrica.Boton(pestanas, "Dice la persona sorda", () => { direccionFrases = "sordo_a_oyente"; LlenarFrases(); }, Tema.SuperficieClara, null, Tema.Pequeno, 0);
            var zonaLista = UIFabrica.Nodo(cuerpoFrases, "Zona");
            UIFabrica.Elemento(zonaLista, 900, -1, -1, 1);
            listaFrases = UIFabrica.Desplazable(zonaLista, "Lista", 12, 8);
            UIFabrica.Estirar((RectTransform)listaFrases.parent);
            UIFabrica.Boton(cuerpoFrases, "Cerrar", () => MostrarFrases(false), Tema.SuperficieClara, null, Tema.Normal);
        }

        Image Indicador(Transform padre, string nombre, out Text texto)
        {
            var nodo = UIFabrica.Nodo(padre, "Indicador" + nombre);
            UIFabrica.Horizontal(nodo.gameObject, 10, 0, false).childAlignment = TextAnchor.MiddleLeft;
            UIFabrica.Elemento(nodo, 96, 230);
            var punto = UIFabrica.Nodo(nodo, "Punto").gameObject.AddComponent<Image>();
            punto.sprite = UIFabrica.Circulo;
            punto.color = Tema.TextoSecundario;
            UIFabrica.Elemento(punto, 28, 28);
            texto = UIFabrica.Texto(nodo, nombre, Tema.Mini, Tema.Texto);
            UIFabrica.Elemento(texto, 96, -1, 1);
            return punto;
        }

        RectTransform Modal(string nombre, out RectTransform cuerpo)
        {
            var velo = UIFabrica.Panel(Raiz, nombre, new Color(0, 0, 0, 0.7f), false).rectTransform;
            UIFabrica.Estirar(velo);
            var caja = UIFabrica.Panel(velo, "Caja", Tema.Fondo).rectTransform;
            UIFabrica.Anclar(caja, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.88f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFabrica.Vertical(caja.gameObject, 20, 32);
            velo.gameObject.SetActive(false);
            cuerpo = caja;
            return velo;
        }

        // ---------------------------------------------------------------- actualización

        public void ConfigurarCamara(Texture textura, double aspecto, bool espejoPantalla)
        {
            espejo = espejoPantalla;
            VistaCamara.texture = textura;
            VistaCamara.color = textura != null ? Color.white : Tema.Fondo;
            VistaCamara.uvRect = espejoPantalla ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            ajusteCamara.aspectRatio = (float)Math.Max(0.1, aspecto);
            Esqueleto.Espejo = espejoPantalla;
        }

        public void MostrarCuadro(CuadroLandmarks c, bool mostrarEsqueleto)
        {
            if (c == null) return;
            if (mostrarEsqueleto) Esqueleto.Mostrar(c.Datos, c.Aspecto);
            else Esqueleto.Limpiar();
            ActualizarBurbuja(c);
        }

        void ActualizarBurbuja(CuadroLandmarks c)
        {
            if (!burbuja.gameObject.activeSelf) return;
            var camRect = VistaCamara.rectTransform.rect;
            Vector3 objetivo;
            if (EmpaquetadorLandmarks.PuntoNormalizado(c.Datos, FormatoLandmarks.IdxCaraReferencia, c.Aspecto, out var u, out var v)
                || EmpaquetadorLandmarks.PuntoNormalizado(c.Datos, FormatoLandmarks.IdxNariz, c.Aspecto, out u, out v))
            {
                if (espejo) u = 1 - u;
                // Encima de la cabeza (≈ 0.12 de la altura de la imagen sobre la nariz)
                var local = new Vector2(camRect.xMin + u * camRect.width, camRect.yMax - (v - 0.12f) * camRect.height);
                objetivo = Raiz.InverseTransformPoint(VistaCamara.rectTransform.TransformPoint(local));
            }
            else
            {
                objetivo = new Vector3(0, -Raiz.rect.height * 0.08f, 0);
            }
            float mitadAncho = Raiz.rect.width / 2f - burbuja.rect.width / 2f - 16;
            objetivo.x = Mathf.Clamp(objetivo.x, -mitadAncho, mitadAncho);
            objetivo.y = Mathf.Clamp(objetivo.y, -Raiz.rect.height * 0.1f, Raiz.rect.height * 0.5f - 160 - burbuja.rect.height);
            posicionBurbuja = Vector3.Lerp(posicionBurbuja, objetivo, 0.25f);
            burbuja.localPosition = posicionBurbuja;
        }

        public void Actualizar()
        {
            if (burbuja.gameObject.activeSelf && finBurbuja > 0 && Time.realtimeSinceStartup > finBurbuja)
                burbuja.gameObject.SetActive(false);
        }

        public void MostrarBurbuja(string texto, float segundos)
        {
            textoBurbuja.text = texto;
            burbuja.gameObject.SetActive(!string.IsNullOrEmpty(texto));
            finBurbuja = segundos > 0 ? Time.realtimeSinceStartup + segundos : -1;
        }

        public void EstadoManos(bool hay, bool senaEnCurso)
        {
            indicadorManos.color = senaEnCurso ? Tema.Acento : hay ? Tema.Exito : Tema.TextoSecundario;
            textoManos.text = senaEnCurso ? "Leyendo…" : hay ? "Manos" : "Sin manos";
        }

        public void EstadoServidor(bool conectado, string detalle)
        {
            indicadorServidor.color = conectado ? Tema.Exito : Tema.Advertencia;
            textoServidor.text = conectado ? "Servidor" : "Sin servidor";
            if (!string.IsNullOrEmpty(detalle)) Avisar(detalle);
        }

        public void Modo(string texto) => textoModo.text = texto;

        public void Avisar(string texto) => aviso.text = texto ?? "";

        public void MostrarEnCurso(IReadOnlyList<SenaReconocida> senas, string palabraDeletreo)
        {
            var partes = senas.Select(s => s.EsDeletreo ? s.Glosa.ToUpperInvariant() : (s.Dudosa ? s.Glosa + "?" : s.Glosa)).ToList();
            if (!string.IsNullOrEmpty(palabraDeletreo)) partes.Add(palabraDeletreo.ToUpperInvariant() + "…");
            var texto = string.Join(" · ", partes);
            textoGlosas.text = texto;
            if (partes.Count > 0) MostrarBurbuja(texto, -1);
            float conf = senas.Count == 0 ? 0f : senas.Average(s => s.Probabilidad);
            Confianza(conf, senas.Count > 0);
            MostrarAlternativas(senas);
        }

        void Confianza(float valor, bool visible)
        {
            var rt = rellenoConfianza.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(visible ? valor : 0f), 1);
            rellenoConfianza.color = valor >= 0.6f ? Tema.Exito : valor >= 0.4f ? Tema.Advertencia : Tema.Peligro;
            etiquetaConfianza.text = visible ? $"confianza {valor:P0}" : "";
        }

        void MostrarAlternativas(IReadOnlyList<SenaReconocida> senas)
        {
            UIFabrica.Limpiar(chips);
            for (int i = senas.Count - 1; i >= 0; i--)
            {
                if (!senas[i].Dudosa || senas[i].EsDeletreo) continue;
                int indice = i;
                var t = UIFabrica.Texto(chips, "¿Quiso decir?", Tema.Mini, Tema.Advertencia);
                UIFabrica.Elemento(t, 84, 210);
                foreach (var alt in senas[i].Alternativas.Take(3))
                {
                    var glosa = alt.Glosa;
                    var b = UIFabrica.Boton(chips, glosa, () => AlCorregir?.Invoke(indice, glosa),
                        glosa == senas[i].Glosa ? Tema.AcentoOscuro : Tema.SuperficieClara, null, Tema.Pequeno, 0);
                    UIFabrica.Elemento(b, 84, 230);
                }
                break;
            }
        }

        public void MostrarTraduccion(string texto, List<string> glosas, string fuente, bool validada, float confianza)
        {
            textoPrincipal.text = texto;
            textoGlosas.text = string.Join(" · ", glosas) + "   " + Etiqueta(fuente, validada);
            Confianza(confianza, true);
            UIFabrica.Limpiar(chips);
            MostrarBurbuja(texto, 7f);
        }

        public void MostrarEmpleado(string texto, string fuente, bool validada, List<string> advertencias)
        {
            textoEmpleado.text = texto;
            fuenteEmpleado.text = "EMPLEADO  " + Etiqueta(fuente, validada);
            if (advertencias != null && advertencias.Count > 0) Avisar(advertencias[0]);
        }

        static string Etiqueta(string fuente, bool validada)
        {
            if (validada) return "<color=#4CC38A>(validada)</color>";
            switch (fuente)
            {
                case "llm": return "<color=#F2B84B>(automática, no validada)</color>";
                case "literal":
                case "local": return "<color=#F2B84B>(literal, no validada)</color>";
                default: return "";
            }
        }

        public void TextoPrincipal(string texto) => textoPrincipal.text = texto;

        public void NivelMicrofono(bool listo, float nivel, bool hablando, bool pulsando)
        {
            if (!listo) { nivelMicrofono.text = "Micrófono no disponible"; return; }
            nivelMicrofono.text = pulsando ? "Escuchando…" : hablando ? "Voz detectada" : $"nivel {nivel * 100f:0}";
            UIFabrica.TextoDeBoton(botonHablar, pulsando ? "Suelte para traducir" : "Mantener para hablar");
        }

        public void FijarInterruptores(bool escuchaAutomatica, bool modoDeletreo)
        {
            escucha.SetIsOnWithoutNotify(escuchaAutomatica);
            deletreo.SetIsOnWithoutNotify(modoDeletreo);
        }

        public void CargarFrases(List<FraseRapida> lista)
        {
            frases = lista ?? new List<FraseRapida>();
            LlenarFrases();
        }

        void LlenarFrases()
        {
            UIFabrica.Limpiar(listaFrases);
            foreach (var f in frases.Where(f => f.Rapida && f.Direccion == direccionFrases))
            {
                var frase = f;
                var b = UIFabrica.Boton(listaFrases, f.Espanol, () => { MostrarFrases(false); AlFraseRapida?.Invoke(frase); },
                    Tema.SuperficieClara, null, Tema.Normal, 110);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.fontStyle = FontStyle.Normal;
            }
        }

        void MostrarEscribir(bool visible) => modalEscribir.gameObject.SetActive(visible);
        void MostrarFrases(bool visible) => modalFrases.gameObject.SetActive(visible);
    }
}
