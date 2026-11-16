#if LSRD_INFERENCIA
using System.Collections.Generic;
using LSRD.App;
using LSRD.Nucleo;
using NUnit.Framework;

namespace LSRD.Pruebas
{
    /// <summary>
    /// Verifica, dentro de Unity, que Inference Engine produce las mismas probabilidades que ONNX
    /// Runtime en Python para los modelos de Resources/LSRD/Modelos. Si se reemplazan los modelos,
    /// regenere los vectores con ml/scripts/generar_vectores_oro.py.
    /// </summary>
    [TestFixture]
    public class PruebasModelo
    {
        static void Comparar(string nombre, int[] forma)
        {
            var modelos = Json.Obj(DatosPrueba.Vectores, "modelos");
            var datos = Json.Obj(modelos, nombre);
            if (datos == null) Assert.Ignore("Los vectores de oro no incluyen el modelo " + nombre);
            var c = ClasificadorOnnx.Cargar(nombre, forma, out var error);
            Assert.IsNotNull(c, error);
            try
            {
                foreach (var o in Json.Lista(datos, "casos"))
                {
                    var caso = (Dictionary<string, object>)o;
                    var entrada = Base64Floats.Decodificar(Json.Cad(caso, "entrada_b64"));
                    var esperado = Json.Lista(caso, "probs");
                    var obtenido = c.Predecir(entrada);
                    Assert.AreEqual(esperado.Count, obtenido.Length, "número de clases");
                    for (int i = 0; i < obtenido.Length; i++)
                        Assert.AreEqual((double)esperado[i], obtenido[i], 1e-3, $"{nombre}: probabilidad {i}");
                }
            }
            finally
            {
                c.Dispose();
            }
        }

        [Test]
        public void ModeloSenas_IgualQueOnnxRuntime() =>
            Comparar("senas", new[] { 1, FormatoLandmarks.LongitudSecuencia, FormatoLandmarks.NPuntos, FormatoLandmarks.NCoordenadas });

        [Test]
        public void ModeloDactilologia_IgualQueOnnxRuntime() =>
            Comparar("dactilologia", new[] { 1, FormatoLandmarks.PuntosMano, 3 });
    }
}
#endif
