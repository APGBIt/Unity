// Subconjunto mínimo de NUnit para ejecutar las pruebas del núcleo fuera de Unity (sin NuGet).
// En Unity se usa el NUnit real del Test Framework; las pruebas solo usan lo que hay aquí.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }

    public sealed class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public static class Assert
    {
        static void Fallar(string m) => throw new AssertionException(m);

        static bool EsNumero(object o) => o is sbyte || o is byte || o is short || o is ushort || o is int || o is uint ||
                                          o is long || o is ulong || o is float || o is double || o is decimal;

        internal static bool Iguales(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (EsNumero(a) && EsNumero(b)) return Convert.ToDouble(a).Equals(Convert.ToDouble(b));
            return a.Equals(b);
        }

        public static void AreEqual(object esperado, object real, string mensaje = null)
        {
            if (!Iguales(esperado, real)) Fallar($"{mensaje}: esperado <{esperado}> pero fue <{real}>");
        }

        public static void AreEqual(double esperado, double real, double delta, string mensaje = null)
        {
            if (double.IsNaN(real) || Math.Abs(esperado - real) > delta)
                Fallar($"{mensaje}: esperado {esperado:R} ± {delta} pero fue {real:R}");
        }

        public static void IsTrue(bool c, string mensaje = null) { if (!c) Fallar(mensaje ?? "se esperaba verdadero"); }
        public static void IsFalse(bool c, string mensaje = null) { if (c) Fallar(mensaje ?? "se esperaba falso"); }
        public static void IsNull(object o, string mensaje = null) { if (o != null) Fallar(mensaje ?? $"se esperaba null y fue {o}"); }
        public static void IsNotNull(object o, string mensaje = null) { if (o == null) Fallar(mensaje ?? "se esperaba un valor"); }
        public static void Greater(double a, double b, string mensaje = null) { if (!(a > b)) Fallar($"{mensaje}: {a} no es mayor que {b}"); }
        public static void Less(double a, double b, string mensaje = null) { if (!(a < b)) Fallar($"{mensaje}: {a} no es menor que {b}"); }

        public static T Throws<T>(Action accion) where T : Exception
        {
            try { accion(); }
            catch (T e) { return e; }
            catch (Exception e) { Fallar($"se esperaba {typeof(T).Name} y se lanzó {e.GetType().Name}"); }
            Fallar($"se esperaba {typeof(T).Name} y no se lanzó nada");
            return null;
        }

        public static void Ignore(string mensaje) => throw new IgnoreException(mensaje);
    }

    public sealed class IgnoreException : Exception { public IgnoreException(string m) : base(m) { } }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable esperado, IEnumerable real, string mensaje = null)
        {
            var a = esperado.Cast<object>().ToList();
            var b = real.Cast<object>().ToList();
            if (a.Count != b.Count) throw new AssertionException($"{mensaje}: {a.Count} elementos esperados, hay {b.Count}");
            for (int i = 0; i < a.Count; i++)
                if (!Assert.Iguales(a[i], b[i]))
                    throw new AssertionException($"{mensaje}: elemento {i}: esperado <{a[i]}> pero fue <{b[i]}>");
        }
    }
}
