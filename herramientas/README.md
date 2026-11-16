# Verificación del código C# sin Unity

Permite revisar la app en una computadora sin Unity (por ejemplo, en integración continua). Solo necesita el SDK de .NET 8; no descarga paquetes.

```bash
cd herramientas/verificacion-csharp/NucleoPruebas
dotnet run -c Release                     # 20 pruebas del núcleo, con paridad Python ↔ C#

cd ../CompilacionUnity
dotnet build -p:Variante=Editor           # todos los scripts + editor + pruebas
dotnet build -p:Variante=Android          # código de Android (voz, permisos, GPU de MediaPipe)
dotnet build -p:Variante=iOSSinPaquetes   # iOS y rutas sin MediaPipe ni Inference Engine
```

- `NucleoPruebas` compila `Assets/LSRD/Scripts/Nucleo` y las pruebas de `Tests/EditMode` con un subconjunto mínimo de NUnit (`NUnitMini.cs`). En Unity se usa el NUnit real.
- `CompilacionUnity` compila todos los scripts contra `Stubs/`, firmas transcritas de Unity 6, MediaPipe Unity Plugin 0.16.3 e Inference Engine 2.6. Detecta errores de sintaxis, tipos y uso de API; **no reemplaza** abrir el proyecto en Unity, porque los stubs no ejecutan nada.

Si cambia `ml/lsrd_ml/formato.py`, la segmentación o el empaquetado, regenere los vectores (`python ml/scripts/generar_vectores_oro.py`) y vuelva a ejecutar estas pruebas.
