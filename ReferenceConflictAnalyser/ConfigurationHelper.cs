using ReferenceConflictAnalyser.DataStructures;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace ReferenceConflictAnalyser
{
    /// <summary>
    /// Proporciona utilidades estáticas para leer información de configuración de .NET relevante
    /// para el análisis de conflictos de ensamblados:
    ///   - Redirecciones de enlace (&lt;bindingRedirect&gt;): indican al CLR que trate una
    ///     versión anterior de un ensamblado como si fuera una versión más reciente, resolviendo
    ///     así conflictos de versión en tiempo de ejecución.
    ///   - Rutas de sondeo (&lt;probing privatePath&gt;): subdirectorios adicionales donde el CLR
    ///     buscará ensamblados referenciados.
    ///   - Sugerencia automática de archivos de configuración a partir de la ruta del ensamblado.
    /// </summary>
    public class ConfigurationHelper
    {
        /// <summary>
        /// Intenta deducir la ruta del archivo de configuración asociado a un ensamblado dado,
        /// siguiendo las convenciones estándar de .NET:
        ///   - Para EXE: busca "&lt;ruta_ensamblado&gt;.config" junto al ejecutable.
        ///   - Para DLL: busca "web.config" en el directorio padre del directorio "bin\",
        ///     asumiendo que la DLL es parte de una aplicación web ASP.NET.
        /// </summary>
        /// <param name="entryAssemblyFilePath">Ruta al ensamblado analizado.</param>
        /// <param name="configFilePath">
        ///   [out] Ruta del archivo de configuración encontrado, o null si no se encontró.
        /// </param>
        /// <returns>true si se encontró un archivo de configuración; false en caso contrario.</returns>
        public static bool TrySuggestConfigFile(string entryAssemblyFilePath, out string configFilePath)
        {
            configFilePath = null;

            if (entryAssemblyFilePath == null)
                return false;

            var fileExtension = Path.GetExtension(entryAssemblyFilePath).ToLower();
            switch (fileExtension)
            {

                case ".exe":
                    {
                        // Convención de aplicaciones de escritorio/consola: el config tiene el mismo
                        // nombre que el EXE con la extensión ".config" añadida.
                        var temp = entryAssemblyFilePath + ".config";
                        if (File.Exists(temp))
                            configFilePath = temp;
                    }
                    break;

                case ".dll":
                    {
                        // Convención ASP.NET: la DLL principal está en "bin\" y el web.config
                        // está en el directorio padre de "bin\".
                        var directory = Path.GetDirectoryName(entryAssemblyFilePath);
                        var re = new Regex(@"\\bin\\?$", RegexOptions.IgnoreCase);
                        directory = re.Replace(directory, "");
                        var temp = Path.Combine(directory, "web.config");
                        if (File.Exists(temp))
                            configFilePath = temp;
                    }
                    break;
            }

            return configFilePath != null;
        }

        /// <summary>
        /// Lee el archivo de configuración indicado y extrae:
        ///   - Las redirecciones de enlace definidas en
        ///     &lt;runtime&gt;&lt;assemblyBinding&gt;&lt;dependentAssembly&gt;&lt;bindingRedirect&gt;.
        ///   - Las rutas de sondeo definidas en
        ///     &lt;runtime&gt;&lt;assemblyBinding&gt;&lt;probing privatePath&gt;.
        ///
        /// Si el archivo no existe o está vacío, devuelve un <see cref="BindingData"/> vacío.
        /// Los errores de formato en el XML (ConfigurationErrorsException) se ignoran silenciosamente
        /// para no interrumpir el análisis cuando el config tiene secciones no estándar.
        /// </summary>
        /// <param name="configFilePath">Ruta al archivo App.config o Web.config.</param>
        /// <returns>
        ///   Un <see cref="BindingData"/> con las redirecciones y subdirectorios encontrados.
        /// </returns>
        public static BindingData GetBindingRedirects(string configFilePath)
        {
            if (string.IsNullOrEmpty(configFilePath) || !File.Exists(configFilePath))
                return new BindingData();

            var redirects = new List<BindingRedirectData>();
            string[] subFolders = null;
            try
            {
                var doc = new XmlDocument();
                doc.Load(configFilePath);

                // El namespace "urn:schemas-microsoft-com:asm.v1" es el namespace estándar del
                // elemento <assemblyBinding> en los archivos de configuración de .NET.
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("bind", "urn:schemas-microsoft-com:asm.v1");

                // Leer las rutas de sondeo (subdirectorios adicionales para buscar ensamblados).
                var probingNode = doc.SelectSingleNode("//bind:probing", nsmgr);
                if (probingNode != null)
                {
                    var str = probingNode.Attributes["privatePath"]?.Value;
                    if (!string.IsNullOrEmpty(str))
                        // Las rutas de sondeo están separadas por ";" en el atributo privatePath.
                        subFolders = str.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries); 
                }

                // Procesar cada elemento <dependentAssembly> que contenga tanto
                // <assemblyIdentity> como <bindingRedirect>.
                var nodes = doc.SelectNodes("//bind:dependentAssembly", nsmgr);
                foreach (XmlElement node in nodes)
                {
                    var assemblyIdentityElem = node.GetElementsByTagName("assemblyIdentity")[0];
                    var bindingRedirectElem = node.GetElementsByTagName("bindingRedirect")[0];
                    if (assemblyIdentityElem == null || bindingRedirectElem == null)
                        continue;

                    var data = new BindingRedirectData()
                    {
                        AssemblyName = assemblyIdentityElem.Attributes["name"].Value,
                        PublicKeyToken = assemblyIdentityElem.Attributes["publicKeyToken"]?.Value,
                        NewVersion = new Version(bindingRedirectElem.Attributes["newVersion"].Value)
                    };

                    // El atributo "oldVersion" puede ser una versión única o un rango "v1-v2".
                    var oldVersions = bindingRedirectElem.Attributes["oldVersion"].Value.Split('-');
                    data.OldVersionLowerBound = GetMainVersion(oldVersions[0]);
                    data.OldVersionUpperBound = oldVersions.Count() > 1 ? GetMainVersion(oldVersions[1]) : data.OldVersionLowerBound;

                    redirects.Add(data);
                }
            }
            catch (ConfigurationErrorsException)
            {
                // Ignorar errores de configuración para no bloquear el análisis.
            }
            return new BindingData(redirects, subFolders);
        }

        /// <summary>
        /// Extrae sólo el componente Major.Minor de una cadena de versión, ignorando
        /// Build y Revision. Esto normaliza las versiones para compararlas con las que
        /// almacena <see cref="BindingRedirectData"/>, que también opera sólo sobre Major.Minor.
        /// </summary>
        /// <param name="versionStr">Cadena de versión en formato "Major.Minor[.Build[.Revision]]".</param>
        /// <returns>Un <see cref="Version"/> con sólo Major y Minor.</returns>
        private static Version GetMainVersion(string versionStr)
        {
            var temp = new Version(versionStr);
            return new Version(temp.Major, temp.Minor);
        }


    }
}
