// Ejecuta todas las pruebas [TestFixture]/[Test] del ensamblado y devuelve 1 si alguna falla.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class Programa
{
    public static int Main()
    {
        int ok = 0, fallas = 0, omitidas = 0;
        var tipos = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<TestFixtureAttribute>() != null).OrderBy(t => t.Name);
        foreach (var tipo in tipos)
        {
            foreach (var m in tipo.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null).OrderBy(m => m.Name))
            {
                var instancia = Activator.CreateInstance(tipo);
                try
                {
                    m.Invoke(instancia, null);
                    ok++;
                    Console.WriteLine($"  ok    {tipo.Name}.{m.Name}");
                }
                catch (TargetInvocationException e) when (e.InnerException is IgnoreException)
                {
                    omitidas++;
                    Console.WriteLine($"  omit  {tipo.Name}.{m.Name}: {e.InnerException.Message}");
                }
                catch (TargetInvocationException e)
                {
                    fallas++;
                    Console.WriteLine($"  FALLA {tipo.Name}.{m.Name}: {e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
                    if (!(e.InnerException is AssertionException)) Console.WriteLine(e.InnerException?.StackTrace);
                }
            }
        }
        Console.WriteLine($"{ok} correctas, {fallas} fallidas, {omitidas} omitidas");
        return fallas == 0 ? 0 : 1;
    }
}
