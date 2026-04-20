using ReferenceConflictAnalyser.DataStructures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser
{
    /// <summary>
    /// Punto de entrada principal de la biblioteca de análisis.
    /// Orquesta el flujo completo de análisis de dependencias de ensamblados .NET:
    /// lectura de referencias, detección de conflictos y generación del grafo DGML.
    ///
    /// El análisis se ejecuta dentro de un AppDomain temporal y aislado para evitar
    /// que los ensamblados cargados durante la inspección contaminen el dominio
    /// de la aplicación principal (y puedan quedar retenidos en memoria).
    /// </summary>
    public class Workflow
    {
        /// <summary>
        /// Genera un grafo de dependencias en formato DGML para el ensamblado especificado.
        ///
        /// El método:
        ///   1. Crea un AppDomain temporal aislado.
        ///   2. Transfiere los parámetros al dominio temporal mediante su almacén de datos.
        ///   3. Invoca <see cref="DoWorkInTempAppDomain"/> en ese dominio.
        ///   4. Recupera el XML DGML resultante y descarga el dominio temporal.
        /// </summary>
        /// <param name="entryAssemblyFilePath">
        ///   Ruta absoluta al ensamblado raíz (DLL o EXE) que se desea analizar.
        /// </param>
        /// <param name="configPath">
        ///   Ruta al archivo de configuración (App.config / Web.config) que contiene
        ///   redirecciones de enlace (<bindingRedirect>). Puede ser null.
        /// </param>
        /// <param name="skipSystemAssemblies">
        ///   Cuando es true (valor predeterminado), excluye del análisis los ensamblados
        ///   cuyo nombre es "mscorlib", "System" o comienza con "System.",
        ///   reduciendo el tamaño del grafo generado.
        /// </param>
        /// <returns>
        ///   Cadena XML con el contenido del documento DGML listo para guardarse en disco
        ///   o abrirse en el editor DGML de Visual Studio.
        /// </returns>
        public static string CreateDependenciesGraph(string entryAssemblyFilePath, string configPath, bool skipSystemAssemblies = true)
        {
            // La base de la aplicación apunta al directorio del ensamblado en ejecución
            // para que el CLR pueda resolver las dependencias del propio analizador.
            var info = new AppDomainSetup()
            {
                ApplicationBase = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            };

            AppDomain tempDomain = null;
            try
            {
                // Se crea un dominio de aplicación secundario con el nombre "TempAppDomain".
                // Todos los ensamblados del ensamblado analizado se cargarán aquí y no
                // interferirán con el dominio principal.
                tempDomain = AppDomain.CreateDomain("TempAppDomain", null, info);

                // Se pasan los parámetros al dominio temporal mediante su diccionario de datos,
                // ya que no es posible pasar parámetros tipados directamente entre dominios
                // sin serialización.
                tempDomain.SetData(EntryAssemblyFilePathKey, entryAssemblyFilePath);
                tempDomain.SetData(SkipSystemAssembliesKey, skipSystemAssemblies);
                tempDomain.SetData(ConfigPathKey, configPath);

                // DoCallBack ejecuta el delegado en el contexto del dominio temporal.
                tempDomain.DoCallBack(DoWorkInTempAppDomain);

                // El resultado (XML DGML) se devuelve también mediante el almacén de datos.
                var graphDgml = tempDomain.GetData(GraphKey);

                return (string)graphDgml;
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                // El dominio temporal se descarga siempre, incluso si hubo excepciones,
                // liberando todos los ensamblados cargados durante el análisis.
                AppDomain.Unload(tempDomain);
            }
        }

        #region private

        // Claves usadas para transferir datos entre el dominio principal y el temporal.
        private const string EntryAssemblyFilePathKey = "EntryAssemblyFilePath";
        private const string SkipSystemAssembliesKey = "SkipSystemAssemblies";
        private const string ConfigPathKey = "configPath";
        private const string GraphKey = "GraphKey";

        /// <summary>
        /// Método que se ejecuta dentro del AppDomain temporal.
        /// Coordina los tres pasos del pipeline de análisis:
        ///   1. <see cref="ReferenceReader"/>: carga recursiva de ensamblados y sus referencias.
        ///   2. <see cref="ReferenceAnalyser"/>: detección de conflictos de versión,
        ///      redirecciones de enlace, ensamblados faltantes y desajustes de arquitectura.
        ///   3. <see cref="GraphBuilder"/>: conversión del resultado en un documento DGML.
        ///
        /// Al finalizar, almacena el XML DGML en el almacén de datos del dominio actual
        /// para que el dominio principal pueda recuperarlo mediante <see cref="AppDomain.GetData"/>.
        /// </summary>
        private static void DoWorkInTempAppDomain()
        {
            // Recuperar los parámetros que el dominio principal depositó antes de invocar este método.
            var entryAssemblyFilePath = AppDomain.CurrentDomain.GetData(EntryAssemblyFilePathKey).ToString();
            var configPath = AppDomain.CurrentDomain.GetData(ConfigPathKey)?.ToString();
            var skipSystemAssemblies = (bool)AppDomain.CurrentDomain.GetData(SkipSystemAssembliesKey);

            // Paso 1: leer redirecciones de enlace y rutas de sondeo del archivo de configuración.
            var bindingData = ConfigurationHelper.GetBindingRedirects(configPath);

            // Paso 2: cargar recursivamente el ensamblado raíz y todas sus dependencias.
            var reader = new ReferenceReader();
            var result = reader.Read(entryAssemblyFilePath, bindingData.SubFolders, skipSystemAssemblies);

            // Paso 3: analizar conflictos, redirecciones, errores de carga y arquitectura.
            var analyser = new ReferenceAnalyser();
            result = analyser.AnalyzeReferences(result, bindingData.BindingRedirects);

            // Paso 4: construir el documento DGML a partir del resultado del análisis.
            var builder = new GraphBuilder();
            var doc = builder.BuildDgml(result);

            // Depositar el XML resultante para que el dominio principal lo recupere.
            AppDomain.CurrentDomain.SetData(GraphKey, doc.OuterXml);
        }

        #endregion

    }
}
