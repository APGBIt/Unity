using System;
using System.Collections.Generic;
using System.Linq;
using LSRD.Nucleo;
using NUnit.Framework;

namespace LSRD.Pruebas
{
    /// <summary>
    /// Pruebas del núcleo C#. Las de paridad comparan con los "vectores de oro" que genera
    /// ml/scripts/generar_vectores_oro.py a partir de la implementación de referencia en Python.
    /// </summary>
    [TestFixture]
    public class PruebasNucleo
    {
        // ------------------------------------------------------------------ paridad con Python

        [Test]
        public void Remuestreo_IgualQuePython()
        {
            foreach (var o in Json.Lista(DatosPrueba.Vectores, "remuestreo"))
            {
                var caso = (Dictionary<string, object>)o;
                int largo = (int)Json.Num(caso, "longitud");
                var esperado = Json.Lista(caso, "indices").Select(x => (int)(double)x).ToArray();
                CollectionAssert.AreEqual(esperado, Remuestreo.Indices(largo), $"longitud {largo}");
            }
        }

        [Test]
        public void Segmentacion_IgualQuePython()
        {
            var seg = Json.Obj(DatosPrueba.Vectores, "segmentacion");
            var pj = Json.Obj(seg, "parametros");
            var p = new ParametrosSegmentacion
            {
                InicioMs = Json.Num(pj, "inicio_ms"), FinMs = Json.Num(pj, "fin_ms"), MinMs = Json.Num(pj, "min_ms"),
                MaxMs = Json.Num(pj, "max_ms"), QuietudMs = Json.Num(pj, "quietud_ms"),
                UmbralVelocidad = Json.Num(pj, "umbral_velocidad"), LineaReposo = Json.Num(pj, "linea_reposo"),
                UsarQuietud = Json.Bool(pj, "usar_quietud"), FactorManoAHombros = Json.Num(pj, "factor_mano_a_hombros"),
            };
            foreach (var o in Json.Lista(seg, "casos"))
            {
                var caso = (Dictionary<string, object>)o;
                string nombre = Json.Cad(caso, "nombre");
                var cuadros = DatosPrueba.Cuadros(caso);
                var tiempos = Json.Lista(caso, "tiempos_ms").Select(x => (double)x).ToList();
                var mediciones = Json.Lista(caso, "mediciones");
                var segmentador = new Segmentador(p);
                var obtenidos = new List<Segmento>();
                float[] anterior = null;
                double? tAnt = null;
                for (int i = 0; i < cuadros.Count; i++)
                {
                    double dt = tAnt.HasValue ? (tiempos[i] - tAnt.Value) / 1000.0 : 0.0;
                    bool activo = MedidorActividad.Medir(cuadros[i], anterior, dt, p, out double vel);
                    var m = (List<object>)mediciones[i];
                    Assert.AreEqual((double)m[0] == 1.0, activo, $"{nombre}: actividad del cuadro {i}");
                    double velEsperada = (double)m[1];
                    if (velEsperada < 0) Assert.IsTrue(double.IsPositiveInfinity(vel), $"{nombre}: velocidad ∞ en {i}");
                    else Assert.AreEqual(velEsperada, vel, 1e-9, $"{nombre}: velocidad del cuadro {i}");
                    var s = segmentador.Procesar(tiempos[i], activo, vel);
                    if (s.HasValue) obtenidos.Add(s.Value);
                    anterior = cuadros[i];
                    tAnt = tiempos[i];
                }
                var final = segmentador.Cerrar();
                if (final.HasValue) obtenidos.Add(final.Value);
                var esperados = Json.Lista(caso, "segmentos");
                Assert.AreEqual(esperados.Count, obtenidos.Count, $"{nombre}: número de segmentos");
                for (int k = 0; k < esperados.Count; k++)
                {
                    var e = (List<object>)esperados[k];
                    Assert.AreEqual((double)e[0], obtenidos[k].InicioMs, 1e-6, $"{nombre}: inicio del segmento {k}");
                    Assert.AreEqual((double)e[1], obtenidos[k].FinMs, 1e-6, $"{nombre}: fin del segmento {k}");
                }
            }
        }

        [Test]
        public void ManoActiva_IgualQuePython()
        {
            foreach (var o in Json.Lista(DatosPrueba.Vectores, "mano_activa"))
            {
                var caso = (Dictionary<string, object>)o;
                var cuadro = Base64Floats.Decodificar(Json.Cad(caso, "cuadro_b64"));
                var destino = new float[FormatoLandmarks.PuntosMano * 3];
                bool hay = EmpaquetadorLandmarks.ManoActiva(cuadro, destino);
                var esperado = Json.Cad(caso, "esperado_b64");
                Assert.AreEqual(esperado != null, hay);
                if (esperado != null) CollectionAssert.AreEqual(Base64Floats.Decodificar(esperado), destino);
            }
        }

        [Test]
        public void Empaquetado_IgualQuePython()
        {
            foreach (var o in Json.Lista(DatosPrueba.Vectores, "empaquetado"))
            {
                var caso = (Dictionary<string, object>)o;
                float[] Opc(string k) => Json.Cad(caso, k) == null ? null : Base64Floats.Decodificar(Json.Cad(caso, k));
                var destino = new float[FormatoLandmarks.ValoresPorCuadro];
                EmpaquetadorLandmarks.Empaquetar(Opc("pose_b64"), Opc("mano_izq_b64"), Opc("mano_der_b64"), Opc("cara_b64"),
                    Json.Num(caso, "aspecto"), destino);
                var esperado = Base64Floats.Decodificar(Json.Cad(caso, "esperado_b64"));
                for (int i = 0; i < destino.Length; i++) Assert.AreEqual(esperado[i], destino[i], 1e-6, $"valor {i}");
            }
        }

        // ------------------------------------------------------------------ cámara

        [Test]
        public void Camara_SinRotacionEsIdentidad()
        {
            var t = TransformacionCamara.Calcular(0, false);
            t.Mapear(0.2f, 0.7f, out var u, out var v);
            Assert.AreEqual(0.2f, u, 1e-6);
            Assert.AreEqual(0.7f, v, 1e-6);
        }

        [Test]
        public void Camara_Rotacion90_EsquinaSuperiorIzquierdaVaALaDerecha()
        {
            // Girar 90° en sentido horario: la esquina superior izquierda de la fuente queda arriba a la derecha.
            var t = TransformacionCamara.Calcular(90, false);
            t.Mapear(1f, 1f, out var u, out var v);
            Assert.AreEqual(0f, u, 1e-6);
            Assert.AreEqual(1f, v, 1e-6);
            t.TamanoSalida(1280, 720, out int ancho, out int alto);
            Assert.AreEqual(720, ancho);
            Assert.AreEqual(1280, alto);
        }

        [Test]
        public void Camara_EspejoVerticalYRotacionesNormalizadas()
        {
            var t = TransformacionCamara.Calcular(-90, true);
            Assert.AreEqual(270, t.Rotacion);
            var c = TransformacionCamara.Calcular(180, true);
            c.Mapear(0.25f, 0.5f, out var u, out var v);
            Assert.AreEqual(0.75f, u, 1e-6);
            Assert.AreEqual(0.5f, v, 1e-6);
        }

        // ------------------------------------------------------------------ frases y dactilología

        static Prediccion Pred(string glosa, float p, double ini, double fin, params string[] otras)
        {
            var probs = new List<float> { p };
            var clases = new List<string> { glosa };
            float resto = (1f - p) / Math.Max(1, otras.Length);
            foreach (var o in otras) { clases.Add(o); probs.Add(resto * 0.99f); }
            var r = Prediccion.DesdeProbabilidades(probs.ToArray(), clases, 3);
            r.InicioMs = ini;
            r.FinMs = fin;
            return r;
        }

        [Test]
        public void ConstructorFrase_IgnoraFondoMarcaDudasYCierraConPausa()
        {
            var f = new ConstructorFrase();
            Assert.IsNull(f.Agregar(Pred("_FONDO", 0.9f, 0, 400)));
            Assert.IsNull(f.Agregar(Pred("HOLA", 0.2f, 500, 900, "ADIOS", "SI", "NO", "QUE")));
            Assert.IsFalse(f.Agregar(Pred("YO", 0.9f, 1000, 1500)).Dudosa);
            var dudosa = f.Agregar(Pred("CITA", 0.5f, 1600, 2100, "CEDULA", "COPIA"));
            Assert.IsTrue(dudosa.Dudosa);
            Assert.AreEqual(3, dudosa.Alternativas.Count);
            Assert.IsNull(f.Actualizar(2500, false));
            Assert.IsNull(f.Actualizar(4000, true));
            var frase = f.Actualizar(3700, false);
            Assert.IsNotNull(frase);
            CollectionAssert.AreEqual(new[] { "YO", "CITA" }, frase.Glosas);
            Assert.IsTrue(frase.TieneDudas);
            Assert.AreEqual(0, f.Actual.Count);
        }

        [Test]
        public void ConstructorFrase_FusionaRepeticionesYPermiteCorregir()
        {
            var f = new ConstructorFrase();
            f.Agregar(Pred("NUM-3", 0.7f, 0, 600));
            f.Agregar(Pred("NUM-3", 0.9f, 800, 1200));
            Assert.AreEqual(1, f.Actual.Count);
            Assert.AreEqual(0.9f, f.Actual[0].Probabilidad, 1e-6);
            f.Agregar(Pred("CITA", 0.5f, 2000, 2400, "CEDULA"));
            f.Corregir(1, "CEDULA");
            Assert.AreEqual("CEDULA", f.Actual[1].Glosa);
            Assert.IsFalse(f.Actual[1].Dudosa);
            f.AgregarDeletreo("ana", 2500, 3000);
            CollectionAssert.AreEqual(new[] { "NUM-3", "CEDULA", "DACT:ana" }, f.Forzar().Glosas);
        }

        [Test]
        public void Dactilologia_ConfirmaLetrasYTerminaPalabra()
        {
            var v = new VotacionDactilologia();
            var letras = new List<string>();
            double t = 0;
            void Cuadros(string glosa, float p, int n)
            {
                for (int i = 0; i < n; i++, t += 33)
                {
                    var l = v.Procesar(t, glosa, p, out _);
                    if (l != null) letras.Add(l);
                }
            }
            Cuadros("LETRA-A", 0.9f, 10);
            Cuadros("LETRA-N", 0.8f, 10);
            Cuadros("LETRA-N", 0.3f, 4);     // transición: suelta la letra
            Cuadros("LETRA-N", 0.8f, 10);    // segunda N
            Cuadros("LETRA-A", 0.9f, 10);
            CollectionAssert.AreEqual(new[] { "a", "n", "n", "a" }, letras);
            string palabra = null;
            for (int i = 0; i < 30 && palabra == null; i++, t += 33) v.Procesar(t, null, 0, out palabra);
            Assert.AreEqual("anna", palabra);
        }

        [Test]
        public void Dactilologia_NoRepiteLetraSostenida()
        {
            var v = new VotacionDactilologia();
            int confirmadas = 0;
            for (int i = 0; i < 60; i++)
                if (v.Procesar(i * 33, "LETRA-B", 0.95f, out _) != null) confirmadas++;
            Assert.AreEqual(1, confirmadas);
        }

        // ------------------------------------------------------------------ audio

        [Test]
        public void DetectorVoz_DetectaUnaFrase()
        {
            int fs = 16000;
            var d = new DetectorVoz(fs);
            var silencio = new float[fs / 2];
            var voz = new float[fs];
            for (int i = 0; i < voz.Length; i++) voz[i] = 0.2f * (float)Math.Sin(2 * Math.PI * 220 * i / fs);
            Assert.IsNull(d.Procesar(silencio, silencio.Length));
            Assert.IsNull(d.Procesar(voz, voz.Length));
            Assert.IsTrue(d.Hablando);
            var frase = d.Procesar(new float[fs], fs);
            Assert.IsNotNull(frase);
            Assert.Greater(frase.Length, fs);   // incluye voz + audio previo + cola
            Assert.IsFalse(d.Hablando);
        }

        [Test]
        public void Wav_CabeceraCorrecta()
        {
            var b = Wav.Codificar(new[] { 0f, 1f, -1f }, 16000);
            Assert.AreEqual(44 + 6, b.Length);
            Assert.AreEqual((byte)'R', b[0]);
            Assert.AreEqual(16000, BitConverter.ToInt32(b, 24));
            Assert.AreEqual(32767, BitConverter.ToInt16(b, 46));
            Assert.AreEqual(-32767, BitConverter.ToInt16(b, 48));
        }

        // ------------------------------------------------------------------ JSON y muestras

        [Test]
        public void Json_IdaYVuelta()
        {
            var d = Json.Objeto("{\"a\": [1, 2.5, -3e2], \"b\": \"señ\\u00e1\\n\", \"c\": {\"d\": true, \"e\": null}}");
            Assert.AreEqual(-300.0, (double)Json.Lista(d, "a")[2], 1e-12);
            Assert.AreEqual("señá\n", Json.Cad(d, "b"));
            Assert.IsTrue(Json.Bool(Json.Obj(d, "c"), "d"));
            var otra = Json.Objeto(Json.Escribir(d));
            Assert.AreEqual(Json.Escribir(d), Json.Escribir(otra));
            Assert.Throws<FormatException>(() => Json.Leer("{\"a\": }"));
        }

        [Test]
        public void Muestra_IdaYVueltaConElFormatoDePython()
        {
            var caso = (Dictionary<string, object>)Json.Lista(Json.Obj(DatosPrueba.Vectores, "segmentacion"), "casos")[0];
            var cuadros = DatosPrueba.Cuadros(caso);
            var m = new MuestraLandmarks { Glosa = "GRACIAS", Firmante = "P001", ConsentimientoPuntos = true };
            for (int i = 0; i < 10; i++) { m.Cuadros.Add(cuadros[i]); m.TiemposMs.Add(i * 33.3); }
            m.Perfil["region"] = "Santiago";
            var r = MuestraLandmarks.DesdeJson(m.AJson());
            Assert.AreEqual("GRACIAS", r.Glosa);
            Assert.AreEqual(10, r.NumeroCuadros);
            CollectionAssert.AreEqual(cuadros[3], r.Cuadros[3]);
            Assert.AreEqual(66.6, r.TiemposMs[2], 1e-9);
            Assert.AreEqual("Santiago", r.Perfil["region"]);
            Assert.IsTrue(r.ConsentimientoPuntos);
        }

        // ------------------------------------------------------------------ traducción local

        [Test]
        public void TraduccionLocal_IgualQueLaLiteralDelServidor()
        {
            var t = new TraductorLocal(DatosPrueba.Vocabulario());
            CollectionAssert.AreEqual(new[] { "BUENOS-DIAS", "QUE", "PODER", "AYUDAR" },
                t.Literal("Buenos días, ¿en qué le puedo ayudar?"));
            CollectionAssert.AreEqual(new[] { "NO-ENTENDER" }, t.Literal("No entiendo"));
            CollectionAssert.AreEqual(new[] { "VOLVER", "NUM-1", "NUM-5", "DIA" }, t.Literal("Vuelva en 15 días"));
            CollectionAssert.AreEqual(new[] { "FIRMAR", "AQUI", "POR-FAVOR" }, t.TextoAGlosas("Firme aquí, por favor.").Glosas);
            CollectionAssert.AreEqual(new[] { "MOSTRAR", "CEDULA", "POR-FAVOR" }, t.Literal("¿Me permite su cédula, por favor?"));
        }

        [Test]
        public void TraduccionLocal_DeletreaYTraduceGlosas()
        {
            var vocab = DatosPrueba.Vocabulario();
            CollectionAssert.AreEqual(new[] { "LETRA-P", "LETRA-E", "LETRA-Ñ", "LETRA-A" }, vocab.Deletreo("Peña"));
            var t = new TraductorLocal(vocab, DatosPrueba.Frases());
            var r = t.TextoAGlosas("Pregunte por Yudelka");
            Assert.IsTrue(r.Plan.Any(p => p.EsDeletreo && p.Palabra == "yudelka" && p.Letras[0] == "LETRA-Y"));
            Assert.AreEqual("¿Baño dónde?", t.GlosasATexto(new[] { "_FONDO", "BAÑO", "DONDE" }).Texto);
            Assert.AreEqual("Yo necesitar cita.", t.GlosasATexto(new[] { "YO", "NECESITAR", "CITA" }).Texto);
        }

        [Test]
        public void Vocabulario_CompletoYCoherente()
        {
            var v = DatosPrueba.Vocabulario();
            Assert.AreEqual(194, v.Entradas.Count);
            Assert.AreEqual(27, v.Entradas.Count(e => e.EsLetra));
            Assert.AreEqual("CEDULA", v.Buscar("cedula"));
            Assert.IsNull(v.Buscar("que"));
            var frases = DatosPrueba.Frases();
            Assert.AreEqual(50, frases.Count);
            Assert.IsTrue(frases.Any(f => f.Rapida && f.Direccion == "sordo_a_oyente"));
        }

        [Test]
        public void Prediccion_OrdenaYDesempata()
        {
            var p = Prediccion.DesdeProbabilidades(new[] { 0.1f, 0.6f, 0.1f, 0.2f }, new[] { "A", "B", "C", "D" }, 3);
            CollectionAssert.AreEqual(new[] { "B", "D", "A" }, p.Candidatas.Select(c => c.Glosa).ToArray());
            Assert.Throws<ArgumentException>(() => Prediccion.DesdeProbabilidades(new[] { 1f }, new[] { "A", "B" }));
        }

        [Test]
        public void Buffer_RecortaPorTiempo()
        {
            var b = new BufferCuadros(5);
            var datos = new float[FormatoLandmarks.ValoresPorCuadro];
            for (int i = 0; i < 8; i++) { datos[0] = i; b.Agregar(i * 100, datos, 1.0); }
            Assert.AreEqual(5, b.Cantidad);
            Assert.AreEqual(300, b[0].TiempoMs, 1e-9);
            var r = b.Recortar(450, 600, 50);
            CollectionAssert.AreEqual(new[] { 4f, 5f, 6f }, r.Select(c => c.Datos[0]).ToArray());
            var plano = Remuestreo.Aplanar(r.Select(c => c.Datos).ToList());
            Assert.AreEqual(FormatoLandmarks.LongitudSecuencia * FormatoLandmarks.ValoresPorCuadro, plano.Length);
        }
    }
}
