using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LSRD.Nucleo;
using UnityEngine;

namespace LSRD.App
{
    /// <summary>
    /// Punto de entrada de la app: carga vocabulario y modelos, construye la interfaz y conecta
    /// cámara → detector → reconocimiento → traducción → voz, y micrófono → servidor → avatar.
    /// </summary>
    public sealed class ControladorApp : MonoBehaviour
    {
        enum Vista { Inicio, Conversacion, Recoleccion, Ajustes }

        AjustesApp ajustes;
        VocabularioLocal vocab;
        List<FraseRapida> frases = new List<FraseRapida>();
        TraductorLocal traductorLocal;
        ClienteLSRD cliente;
        MotorReconocimiento motor;
        BibliotecaSenas biblioteca;
        ColaEnvio cola;

        FuenteCamara camara;
        IDetectorLandmarks detector;
        MonoBehaviour componenteDetector;
        Microfono microfono;
        VozSintetizada voz;
        AvatarEsquematico avatar, avatarRevision;
        AvatarHumanoide humanoide;
        ReproductorPlan reproductor;

        PantallaInicio inicio;
        PantallaConversacion conversacion;
        PantallaRecoleccion recoleccion;
        PantallaAjustes pantallaAjustes;
        Vista vista;

        SesionRecoleccion sesion;
        CuadroLandmarks ultimoCuadro;
        bool capturando, iniciandoCaptura;
        string configuracionCaptura = "";
        string estadoCaptura = "";
        float proximaSalud;
        double aspectoSimulado = -1;

        // ------------------------------------------------------------------ arranque

        void Awake()
        {
            Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            ajustes = AjustesApp.Cargar();

            var csv = Resources.Load<TextAsset>("LSRD/Datos/vocabulario_mvp");
            vocab = csv != null ? VocabularioLocal.DesdeCsv(csv.text) : VocabularioLocal.DesdeCsv("glosa,espanol,sinonimos,categoria,prioridad,tipo,modelo,estado\n");
            var csvFrases = Resources.Load<TextAsset>("LSRD/Datos/frases_mvp");
            if (csvFrases != null) frases = VocabularioLocal.FrasesDesdeCsv(csvFrases.text);
            traductorLocal = new TraductorLocal(vocab, frases);

            cliente = new ClienteLSRD(ajustes.UrlServidor, ajustes.Clave);
            biblioteca = new BibliotecaSenas();
            biblioteca.Cargar();
            cola = new ColaEnvio(Path.Combine(Application.persistentDataPath, "recolecciones"));

            motor = new MotorReconocimiento(ajustes);
            motor.CargarModelos();
            motor.AlSena += (p, s) => RefrescarEnCurso();
            motor.AlLetra += l => RefrescarEnCurso();
            motor.AlCambio += RefrescarEnCurso;
            motor.AlFrase += AlFraseReconocida;

            camara = gameObject.AddComponent<FuenteCamara>();
            microfono = gameObject.AddComponent<Microfono>();
            microfono.AlFrase += AlVozEmpleado;
            voz = gameObject.AddComponent<VozSintetizada>();
            voz.Activa = ajustes.HablarTraduccion;

            ConstruirInterfaz();
            IrA(Vista.Inicio);
        }

        void Start() => StartCoroutine(ComprobarServidor());

        void ConstruirInterfaz()
        {
            var canvas = UIFabrica.CrearCanvas("LSRD-Interfaz");

            inicio = new PantallaInicio(canvas.transform);
            inicio.AlConversacion += () => IrA(Vista.Conversacion);
            inicio.AlRecoleccion += () => IrA(Vista.Recoleccion);
            inicio.AlAjustes += () => IrA(Vista.Ajustes);

            conversacion = new PantallaConversacion(canvas.transform);
            conversacion.AlVolver += () => IrA(Vista.Inicio);
            conversacion.AlEnviar += () => { var f = motor.Forzar(); if (f != null) AlFraseReconocida(f); };
            conversacion.AlBorrar += () => { motor.Frases.Deshacer(); motor.Votacion.Borrar(); RefrescarEnCurso(); };
            conversacion.AlCorregir += (i, g) => { motor.Frases.Corregir(i, g); RefrescarEnCurso(); };
            conversacion.AlDeletreo += v =>
            {
                motor.ModoDeletreo = v;
                if (v && !motor.PuedeDeletrear) conversacion.Avisar("No hay modelo de dactilología cargado.");
                ActualizarModo();
            };
            conversacion.AlEscuchaAutomatica += v =>
            {
                ajustes.EscuchaAutomatica = v;
                microfono.EscuchaAutomatica = v;
                if (v) AsegurarMicrofono();
            };
            conversacion.AlEmpezarHablar += () => { AsegurarMicrofono(); if (microfono.Listo) microfono.EmpezarPulsacion(); };
            conversacion.AlTerminarHablar += () => microfono.TerminarPulsacion();
            conversacion.AlEscribir += TraducirTextoEmpleado;
            conversacion.AlFraseRapida += UsarFraseRapida;
            conversacion.AlRepetirAvatar += () => reproductor.Repetir();
            conversacion.AlVelocidadAvatar += v => { reproductor.Velocidad = v; ajustes.VelocidadAvatar = v; };
            conversacion.CargarFrases(frases);

            recoleccion = new PantallaRecoleccion(canvas.transform, ajustes.CodigoFirmante);
            recoleccion.AlVolver += () => IrA(Vista.Inicio);
            recoleccion.AlComenzar += ComenzarSesion;
            recoleccion.AlAceptar += () =>
            {
                var ruta = sesion?.Aceptar();
                if (ruta != null && cliente.Conectado) StartCoroutine(EnviarPendientes(false));
            };
            recoleccion.AlRepetir += () => sesion?.Repetir();
            recoleccion.AlSaltar += () => sesion?.Saltar();
            recoleccion.AlTerminar += () =>
            {
                sesion?.Terminar();
                recoleccion.MostrarPaso(false);
                ActualizarEnvio();
            };
            recoleccion.AlEnviar += () => StartCoroutine(EnviarPendientes(true));

            pantallaAjustes = new PantallaAjustes(canvas.transform, ajustes);
            pantallaAjustes.AlVolver += () => IrA(Vista.Inicio);
            pantallaAjustes.AlGuardar += () => { AplicarAjustes(); IrA(Vista.Inicio); };
            pantallaAjustes.AlProbarConexion += () =>
            {
                AplicarAjustes();
                pantallaAjustes.Resultado("Probando…");
                StartCoroutine(cliente.Salud(e => pantallaAjustes.Resultado(e == null
                    ? $"Conectado. Traducción automática: {(cliente.TraduccionLlm ? "sí" : "no")}. Voz a texto: {(cliente.VozDisponible ? "sí" : "no")}."
                    : "No se pudo conectar: " + e)));
            };
            pantallaAjustes.AlSincronizarAvatar += () =>
            {
                AplicarAjustes();
                pantallaAjustes.Resultado("Descargando grabaciones del avatar…");
                StartCoroutine(biblioteca.Sincronizar(cliente, (n, e) =>
                {
                    pantallaAjustes.Resultado(e == null ? $"{n} señas nuevas. Total: {biblioteca.Cantidad}." : "Error: " + e);
                    avatar.MostrarReposo(ReferenciaReposo());
                }));
            };

            avatar = conversacion.Avatar.gameObject.AddComponent<AvatarEsquematico>();
            avatar.Grafico = conversacion.Avatar;
            humanoide = AvatarHumanoide.Crear(transform);
            if (humanoide != null)
            {
                conversacion.Avatar3D.texture = humanoide.Imagen;
                conversacion.Avatar3D.enabled = true;
            }
            reproductor = conversacion.Raiz.gameObject.AddComponent<ReproductorPlan>();
            reproductor.Esquematico = avatar;
            reproductor.Humanoide = humanoide;
            reproductor.Biblioteca = biblioteca;
            reproductor.Rotulo = conversacion.RotuloAvatar;
            reproductor.Tarjeta = conversacion.TarjetaAvatar;
            reproductor.Velocidad = ajustes.VelocidadAvatar;
            reproductor.Reposo = ReferenciaReposo;
            avatar.MostrarReposo(ReferenciaReposo());

            avatarRevision = recoleccion.Revision.gameObject.AddComponent<AvatarEsquematico>();
            avatarRevision.Grafico = recoleccion.Revision;
        }

        void AplicarAjustes()
        {
            ajustes.Guardar();
            cliente.Url = ajustes.UrlServidor.Trim().TrimEnd('/');
            cliente.Clave = ajustes.Clave;
            motor.Configurar(ajustes);
            voz.Activa = ajustes.HablarTraduccion;
            reproductor.Velocidad = ajustes.VelocidadAvatar;
            microfono.CambiarUmbral(ajustes.UmbralVoz);
            camara.RotacionForzada = ajustes.RotacionForzada;
        }

        // ------------------------------------------------------------------ navegación y captura

        void IrA(Vista v)
        {
            vista = v;
            inicio.Raiz.gameObject.SetActive(v == Vista.Inicio);
            conversacion.Raiz.gameObject.SetActive(v == Vista.Conversacion);
            recoleccion.Raiz.gameObject.SetActive(v == Vista.Recoleccion);
            pantallaAjustes.Raiz.gameObject.SetActive(v == Vista.Ajustes);
            if (v == Vista.Conversacion || v == Vista.Recoleccion)
            {
                if (v == Vista.Conversacion)
                {
                    motor.Reiniciar();
                    conversacion.FijarInterruptores(ajustes.EscuchaAutomatica, motor.ModoDeletreo);
                    microfono.EscuchaAutomatica = ajustes.EscuchaAutomatica;
                    if (ajustes.EscuchaAutomatica) AsegurarMicrofono();
                    ActualizarModo();
                }
                else
                {
                    recoleccion.MostrarPaso(sesion != null && sesion.FaseActual != SesionRecoleccion.Fase.Terminada);
                    ActualizarEnvio();
                }
                StartCoroutine(IniciarCaptura());
            }
            else
            {
                DetenerCaptura();
                if (v == Vista.Inicio) MostrarEstadoInicio();
            }
        }

        string FirmaCaptura() => $"{ajustes.DetectorSimulado}|{ajustes.CamaraFrontal}|{ajustes.RotacionForzada}|{ajustes.UsarGpuMediaPipe}";

        IEnumerator IniciarCaptura()
        {
            if (iniciandoCaptura) yield break;
            if (capturando && configuracionCaptura == FirmaCaptura()) yield break;
            iniciandoCaptura = true;
            DetenerCaptura();
            estadoCaptura = "";
            bool simulado = ajustes.DetectorSimulado;
#if !LSRD_MEDIAPIPE
            simulado = true;
            estadoCaptura = "MediaPipe no está instalado: se usa el detector simulado. ";
#endif
            if (!simulado)
            {
                yield return camara.Iniciar(ajustes.CamaraFrontal, ajustes.RotacionForzada);
                if (camara.Error != null)
                {
                    estadoCaptura = camara.Error + " Se usa el detector simulado. ";
                    simulado = true;
                }
            }
#if LSRD_MEDIAPIPE
            if (!simulado) componenteDetector = gameObject.AddComponent<DetectorHolistico>();
#endif
            if (componenteDetector == null) componenteDetector = gameObject.AddComponent<DetectorSimulado>();
            detector = (IDetectorLandmarks)componenteDetector;
            detector.AlCuadro += AlCuadro;
            yield return detector.Iniciar(camara, ajustes);
            estadoCaptura += detector.Estado;
            capturando = true;
            configuracionCaptura = FirmaCaptura();
            iniciandoCaptura = false;
            conversacion.Avisar(AvisoGeneral());
            recoleccion.Estado(detector.Listo ? "Lista para grabar." : detector.Estado);
        }

        void DetenerCaptura()
        {
            if (detector != null)
            {
                detector.AlCuadro -= AlCuadro;
                detector.Detener();
            }
            if (componenteDetector != null) Destroy(componenteDetector);
            componenteDetector = null;
            detector = null;
            camara.Detener();
            capturando = false;
            ultimoCuadro = null;
        }

        void AlCuadro(CuadroLandmarks c)
        {
            ultimoCuadro = c;
            if (vista == Vista.Conversacion) motor.Procesar(c);
            else if (vista == Vista.Recoleccion && sesion != null) sesion.Procesar(c);
        }

        void Update()
        {
            if (vista == Vista.Conversacion)
            {
                if (camara.Salida != null && conversacion.VistaCamara.texture != camara.Salida)
                    conversacion.ConfigurarCamara(camara.Salida, camara.Aspecto, ajustes.EspejoPantalla && camara.EsFrontal);
                else if (camara.Salida == null && ultimoCuadro != null && ultimoCuadro.Aspecto != aspectoSimulado)
                {
                    aspectoSimulado = ultimoCuadro.Aspecto;
                    conversacion.ConfigurarCamara(null, aspectoSimulado, false);
                }
                conversacion.MostrarCuadro(ultimoCuadro, ajustes.MostrarEsqueleto);
                conversacion.EstadoManos(motor.HayManos, motor.SenaEnCurso);
                conversacion.NivelMicrofono(microfono.Listo, microfono.Nivel, microfono.HablandoDetectado, microfono.Pulsando);
                conversacion.Actualizar();
                microfono.Silenciado = voz.Hablando || reproductor.Reproduciendo;
                if (Time.realtimeSinceStartup > proximaSalud)
                {
                    proximaSalud = Time.realtimeSinceStartup + 30f;
                    StartCoroutine(ComprobarServidor());
                }
            }
            else if (vista == Vista.Recoleccion)
            {
                if (camara.Salida != null && recoleccion.VistaCamara.texture != camara.Salida)
                    recoleccion.ConfigurarCamara(camara.Salida, camara.Aspecto, ajustes.EspejoPantalla && camara.EsFrontal);
                if (ultimoCuadro != null) recoleccion.Esqueleto.Mostrar(ultimoCuadro.Datos, ultimoCuadro.Aspecto);
                if (sesion != null && sesion.FaseActual == SesionRecoleccion.Fase.Esperando)
                {
                    if (sesion.GlosaActual == SesionRecoleccion.Fondo)
                        recoleccion.Estado($"Grabando movimientos naturales… {sesion.ProgresoFondo:P0}");
                    else
                        recoleccion.Estado(sesion.HayManos ? "Haga la seña y luego baje las manos." : "Esperando la seña…");
                }
            }
        }

        // ------------------------------------------------------------------ persona sorda → español

        void RefrescarEnCurso() => conversacion.MostrarEnCurso(motor.Frases.Actual, motor.Votacion.PalabraEnCurso);

        void AlFraseReconocida(Frase f)
        {
            var glosas = f.Glosas;
            conversacion.TextoPrincipal("Traduciendo…");
            if (cliente.Conectado)
            {
                StartCoroutine(cliente.GlosasATexto(glosas, (r, e) =>
                {
                    if (r == null) r = traductorLocal.GlosasATexto(glosas);
                    MostrarTraduccion(r, f);
                }));
            }
            else
            {
                MostrarTraduccion(traductorLocal.GlosasATexto(glosas), f);
            }
        }

        void MostrarTraduccion(ResultadoTexto r, Frase f)
        {
            conversacion.MostrarTraduccion(r.Texto, f.Glosas, r.Fuente, r.Validada, f.ConfianzaMedia);
            if (ajustes.HablarTraduccion) voz.Hablar(r.Texto);
            Vibrar();
        }

        void UsarFraseRapida(FraseRapida f)
        {
            if (f.Direccion == "sordo_a_oyente")
            {
                conversacion.MostrarTraduccion(f.Espanol, f.Glosas, f.Validada ? "memoria" : "rapida", f.Validada, 1f);
                voz.Hablar(f.Espanol);
            }
            else
            {
                TraducirTextoEmpleado(f.Espanol);
            }
        }

        // ------------------------------------------------------------------ empleado → avatar

        void AsegurarMicrofono()
        {
            if (!microfono.Listo && microfono.Error == null) StartCoroutine(microfono.Iniciar(ajustes.UmbralVoz));
            else if (microfono.Error != null) conversacion.Avisar(microfono.Error);
        }

        void AlVozEmpleado(byte[] wav)
        {
            if (!cliente.Conectado)
            {
                conversacion.Avisar("Sin servidor no hay voz a texto. Use «Escribir» o las frases rápidas.");
                return;
            }
            conversacion.MostrarEmpleado("Escuchando…", "", false, null);
            StartCoroutine(cliente.VozASenas(wav, (r, e) =>
            {
                if (r == null)
                {
                    conversacion.MostrarEmpleado("", "", false, null);
                    conversacion.Avisar("No se pudo transcribir: " + e);
                    return;
                }
                if (string.IsNullOrWhiteSpace(r.Transcripcion ?? r.Texto)) { conversacion.MostrarEmpleado("(no se entendió)", "", false, null); return; }
                conversacion.MostrarEmpleado(r.Transcripcion ?? r.Texto, r.Fuente, r.Validada, r.Advertencias);
                reproductor.Reproducir(r.Plan);
            }));
        }

        void TraducirTextoEmpleado(string texto)
        {
            if (cliente.Conectado)
            {
                StartCoroutine(cliente.TextoAGlosas(texto, (r, e) =>
                {
                    if (r == null) r = traductorLocal.TextoAGlosas(texto);
                    conversacion.MostrarEmpleado(texto, r.Fuente, r.Validada, r.Advertencias);
                    reproductor.Reproducir(r.Plan);
                }));
            }
            else
            {
                var r = traductorLocal.TextoAGlosas(texto);
                conversacion.MostrarEmpleado(texto, r.Fuente, r.Validada, r.Advertencias);
                reproductor.Reproducir(r.Plan);
            }
        }

        // ------------------------------------------------------------------ recolección

        void ComenzarSesion(PerfilFirmante perfil, bool consentimientoAvatar, int conjunto, int repeticiones)
        {
            ajustes.CodigoFirmante = perfil.Codigo;
            ajustes.Guardar();
            var glosas = new List<string>();
            switch (conjunto)
            {
                case 1:
                    glosas.AddRange(vocab.Entradas.Where(e => e.Tipo == "seña" || e.Tipo == "numero").Select(e => e.Glosa));
                    break;
                case 2:
                    glosas.AddRange(vocab.Entradas.Where(e => e.EsLetra).Select(e => e.Glosa));
                    break;
                case 3:
                    glosas.AddRange(vocab.Entradas.Where(e => e.Tipo == "numero").Select(e => e.Glosa));
                    break;
                default:
                    glosas.AddRange(vocab.Entradas.Where(e => (e.Tipo == "seña" || e.Tipo == "numero") && e.Prioridad == 1).Select(e => e.Glosa));
                    break;
            }
            if (conjunto != 2) { glosas.Insert(0, SesionRecoleccion.Fondo); glosas.Add(SesionRecoleccion.Fondo); }
            sesion = new SesionRecoleccion(glosas, perfil, consentimientoAvatar, repeticiones, cola.Pendientes);
            sesion.InformarCamara(camara.EsFrontal, camara.Salida != null ? camara.Salida.width : 0, camara.Salida != null ? camara.Salida.height : 0);
            sesion.AlCambio += AlCambioSesion;
            recoleccion.MostrarPaso(true);
            AlCambioSesion();
        }

        void AlCambioSesion()
        {
            recoleccion.MostrarSesion(sesion, vocab);
            if (sesion.FaseActual == SesionRecoleccion.Fase.Revision)
            {
                var m = sesion.UltimaMuestra;
                void Bucle()
                {
                    if (sesion != null && sesion.UltimaMuestra == m && sesion.FaseActual == SesionRecoleccion.Fase.Revision)
                        avatarRevision.Reproducir(m, Bucle);
                }
                Bucle();
                recoleccion.Estado($"Duración {m.DuracionMs / 1000.0:0.0} s · {m.NumeroCuadros} cuadros. ¿Se ve bien?");
            }
            else
            {
                avatarRevision.Detener();
            }
        }

        IEnumerator EnviarPendientes(bool informar)
        {
            if (cola.Enviando) yield break;
            if (informar) recoleccion.Envio("Enviando…");
            yield return cola.EnviarTodo(cliente, (n, e) =>
            {
                if (informar || e != null)
                    recoleccion.Envio(e == null ? $"{n} grabaciones enviadas." : $"{n} enviadas. Último error: {e}");
                ActualizarEnvio(e == null);
            });
        }

        void ActualizarEnvio(bool soloSiVacio = false)
        {
            if (soloSiVacio && cola.CantidadPendiente > 0) return;
            recoleccion.Envio($"Grabaciones en este teléfono: {cola.CantidadPendiente} pendientes de envío, {cola.CantidadEnviada} enviadas.");
        }

        // ------------------------------------------------------------------ estado

        IEnumerator ComprobarServidor()
        {
            yield return cliente.Salud(e =>
            {
                conversacion.EstadoServidor(cliente.Conectado, null);
                if (vista == Vista.Inicio) MostrarEstadoInicio();
            });
        }

        void ActualizarModo()
        {
            var partes = new List<string>();
            if (motor.ModoDeletreo) partes.Add("DELETREO");
            if (motor.ModelosDemostracion) partes.Add("MODELO DEMO");
            if (componenteDetector is DetectorSimulado) partes.Add("SIMULADO");
            conversacion.Modo(string.Join(" · ", partes));
        }

        string AvisoGeneral()
        {
            var sb = new StringBuilder();
            if (!motor.PuedeReconocerSenas) sb.Append("Sin modelo de señas. ");
            if (motor.ModelosDemostracion) sb.Append("Modelo de demostración: no reconoce LSRD. ");
            if (!string.IsNullOrEmpty(estadoCaptura) && (componenteDetector is DetectorSimulado)) sb.Append(estadoCaptura);
            return sb.ToString();
        }

        void MostrarEstadoInicio()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>Estado</b>");
            sb.AppendLine("Modelos: " + motor.EstadoModelos);
#if LSRD_MEDIAPIPE
            sb.AppendLine("Detector: MediaPipe instalado" + (ajustes.DetectorSimulado ? " (usando simulado por ajustes)" : ""));
#else
            sb.AppendLine("Detector: MediaPipe NO instalado (se usará el simulado)");
#endif
            sb.AppendLine($"Servidor: {(string.IsNullOrEmpty(cliente.Url) ? "sin configurar" : cliente.Url)} — " +
                          (cliente.Conectado ? $"conectado (traducción automática: {(cliente.TraduccionLlm ? "sí" : "no")}, voz: {(cliente.VozDisponible ? "sí" : "no")})" : "sin conexión (traducción literal local)"));
            sb.AppendLine($"Vocabulario: {vocab.Entradas.Count} entradas · frases validadas: {frases.Count(f => f.Validada)} de {frases.Count}");
            sb.AppendLine($"Avatar: {biblioteca.Cantidad} señas grabadas" + (humanoide != null ? " + avatar 3D" : ""));
            sb.AppendLine($"Grabaciones pendientes de envío: {cola.CantidadPendiente}");
            inicio.MostrarEstado(sb.ToString());
        }

        void Vibrar()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (ajustes.Vibrar) Handheld.Vibrate();
#endif
        }

        /// <summary>Postura neutra para el avatar: la biblioteca o, si está vacía, una grabación de simulación.</summary>
        MuestraLandmarks ReferenciaReposo()
        {
            var r = biblioteca.Referencia();
            if (r != null) return r;
            foreach (var t in Resources.LoadAll<TextAsset>("LSRD/Simulacion"))
            {
                try { return MuestraLandmarks.DesdeJson(t.text); }
                catch (System.Exception) { /* siguiente */ }
            }
            return null;
        }

        void OnDestroy() => motor?.Dispose();
    }
}
