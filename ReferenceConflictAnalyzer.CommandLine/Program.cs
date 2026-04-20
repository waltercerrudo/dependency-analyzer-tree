using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyzer.CommandLine
{
    /// <summary>
    /// Punto de entrada de la aplicación de línea de comandos para el análisis de conflictos
    /// de dependencias de ensamblados .NET.
    ///
    /// Esta aplicación es una interfaz de línea de comandos para la biblioteca
    /// <c>ReferenceConflictAnalyser</c>. Permite analizar ensamblados en entornos sin interfaz
    /// gráfica (servidores de CI/CD, scripts de automatización, etc.) y generar el archivo DGML
    /// resultante en un directorio de salida especificado.
    ///
    /// Modo de uso:
    /// <code>
    ///   ReferenceConflictAnalyzer.CommandLine.exe
    ///     -file=C:\ruta\al\ensamblado.dll
    ///     -config=C:\ruta\al\app.config
    ///     -ignoreSystemAssemblies=true
    ///     -output=C:\ruta\de\salida
    /// </code>
    /// </summary>
    class Program
    {
        /// <summary>
        /// Punto de entrada principal. Gestiona dos casos:
        ///   1. Si no se pasan argumentos, o el primer argumento es "?" o "help",
        ///      muestra la ayuda con los parámetros disponibles y espera una tecla.
        ///   2. En cualquier otro caso, delega en <see cref="GenerateDgmlFileCommand.Run"/>
        ///      para ejecutar el análisis.
        ///
        /// Los errores no controlados se capturan, se muestran por consola y se pausa la
        /// ejecución para que el usuario pueda leer el mensaje antes de que la ventana se cierre.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos en formato -nombre=valor.</param>
        static void Main(string[] args)
        {
            try
            {
                if (args == null
                    || !args.Any()
                    || args[0] == "?"
                    || args[0] == "help")
                {
                    Console.WriteLine(@"Parameters: 

 -file - (mandatory) path to assembly (DLL or executable) to analyze;
 -config - (optional) path to related config file;
 -ignoreSystemAssemblies - (optional, default value is 'true') exclude from the analysis assemblies with names starting with 'System';
 -output - (mandatory) directory to put genegated DGML file to.");
                    Console.ReadKey();
                }
                else
                    new GenerateDgmlFileCommand().Run(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.ReadKey();
            }
        }

        
    }
}
