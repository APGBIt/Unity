using System;
using System.Collections.Generic;
using System.IO;
using LSRD.Nucleo;

namespace LSRD.Pruebas
{
    /// <summary>Localiza los archivos de prueba tanto en Unity como en la verificación con .NET.</summary>
    public static class DatosPrueba
    {
        static Dictionary<string, object> vectores;

        public static string Ruta(string relativa)
        {
            var env = Environment.GetEnvironmentVariable("LSRD_PROYECTO_UNITY");
            var candidatos = new List<string>();
            if (!string.IsNullOrEmpty(env)) candidatos.Add(Path.Combine(env, relativa));
            candidatos.Add(Path.Combine(Directory.GetCurrentDirectory(), relativa));
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                candidatos.Add(Path.Combine(dir.FullName, "unity", "LSRD-AR", relativa));
                dir = dir.Parent;
            }
            foreach (var c in candidatos) if (File.Exists(c)) return c;
            throw new FileNotFoundException("No se encontró " + relativa + " (defina LSRD_PROYECTO_UNITY)");
        }

        public static string Leer(string relativa) => File.ReadAllText(Ruta(relativa));

        public static Dictionary<string, object> Vectores =>
            vectores ?? (vectores = Json.Objeto(Leer("Assets/LSRD/Tests/Datos/vectores_oro.json")));

        public static VocabularioLocal Vocabulario() =>
            VocabularioLocal.DesdeCsv(Leer("Assets/LSRD/Resources/LSRD/Datos/vocabulario_mvp.csv"));

        public static List<FraseRapida> Frases() =>
            VocabularioLocal.FrasesDesdeCsv(Leer("Assets/LSRD/Resources/LSRD/Datos/frases_mvp.csv"));

        public static List<float[]> Cuadros(Dictionary<string, object> caso, string clave = "cuadros_b64", string claveForma = "forma")
        {
            var plano = Base64Floats.Decodificar(Json.Cad(caso, clave));
            int n = plano.Length / FormatoLandmarks.ValoresPorCuadro;
            var salida = new List<float[]>(n);
            for (int i = 0; i < n; i++)
            {
                var c = new float[FormatoLandmarks.ValoresPorCuadro];
                Array.Copy(plano, i * c.Length, c, 0, c.Length);
                salida.Add(c);
            }
            return salida;
        }
    }
}
