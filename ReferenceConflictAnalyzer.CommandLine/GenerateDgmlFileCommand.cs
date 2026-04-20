using ReferenceConflictAnalyser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyzer.CommandLine
{
    /// <summary>
    /// Comando que encapsula la lógica de análisis de dependencias desde la línea de comandos.
    /// Parsea los argumentos de entrada, invoca el pipeline de análisis a través de
    /// <see cref="Workflow.CreateDependenciesGraph"/> y escribe el resultado en un archivo .dgml
    /// con nombre basado en la fecha y hora de ejecución.
    ///
    /// Los parámetros aceptados son:
    ///   -file=&lt;ruta&gt;              (obligatorio) Ensamblado a analizar.
    ///   -config=&lt;ruta&gt;            (opcional)   Archivo de configuración con bindingRedirects.
    ///   -ignoreSystemAssemblies=true|false (opcional, default: true) Excluir ensamblados del sistema.
    ///   -output=&lt;directorio&gt;      (obligatorio) Directorio donde se guardará el DGML generado.
    /// </summary>
    class GenerateDgmlFileCommand
    {
        /// <summary>
        /// Ejecuta el análisis de dependencias con los argumentos proporcionados:
        ///   1. Valida y parsea los parámetros con <see cref="ValidateParameters"/>.
        ///   2. Llama a <see cref="Workflow.CreateDependenciesGraph"/> para obtener el XML DGML.
        ///   3. Guarda el resultado en un archivo .dgml cuyo nombre incluye la fecha y hora
        ///      de ejecución para facilitar la trazabilidad de múltiples análisis.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos en formato -nombre=valor.</param>
        public void Run(string[] args)
        {
            ValidateParameters(args);

            var dgml = Workflow.CreateDependenciesGraph(_filePath, _configPath, _ignoreSystemAssemblies);
            // El nombre de archivo incluye timestamp para evitar sobreescribir ejecuciones anteriores.
            var outputFileName = Path.Combine(_outputFolder, $"{DateTime.Now.ToString("dd-MM-yyyy hh-MM-ss")}.dgml");
            File.WriteAllText(outputFileName, dgml);
        }

        #region private 

        /// <summary>
        /// Patrón de expresión regular para parsear argumentos en el formato "-nombre=valor".
        /// El grupo "name" captura el nombre del parámetro y el grupo "value" captura su valor.
        /// </summary>
        private const string ParameterPattern = "^-(?<name>\\w+)=(?<value>.*)$";
        private string _filePath;
        private string _configPath;
        private bool _ignoreSystemAssemblies = true;
        private string _outputFolder;

        /// <summary>
        /// Parsea los argumentos de línea de comandos usando la expresión regular
        /// <see cref="ParameterPattern"/> y los asigna a los campos privados correspondientes.
        /// Tras el parseo, valida que los parámetros obligatorios estén presentes.
        ///
        /// Parámetros reconocidos:
        ///   - "file": ruta al ensamblado a analizar (obligatorio).
        ///   - "config": ruta al archivo de configuración (opcional).
        ///   - "ignoreSystemAssemblies": true/false, excluir ensamblados del sistema (opcional).
        ///   - "output": directorio de salida para el DGML generado (obligatorio).
        ///
        /// Los argumentos que no coinciden con el patrón se ignoran silenciosamente.
        /// </summary>
        /// <param name="args">Array de argumentos de línea de comandos.</param>
        /// <exception cref="ArgumentException">
        ///   Si el valor de "ignoreSystemAssemblies" no es un booleano válido.
        /// </exception>
        /// <exception cref="Exception">
        ///   Si falta el parámetro obligatorio "file" o "output".
        /// </exception>
        private void ValidateParameters(string[] args)
        {
            var re = new Regex(ParameterPattern, RegexOptions.IgnoreCase);

            foreach (var arg in args)
            {
                var match = re.Match(arg);
                if (match.Success)
                {
                    var value = match.Groups["value"].Value;
                    switch (match.Groups["name"].Value)
                    {
                        case "file":
                            _filePath = value;
                            break;

                        case "config":
                            _configPath = value;
                            break;

                        case "ignoreSystemAssemblies":
                            bool t;
                            if (bool.TryParse(value, out t))
                                _ignoreSystemAssemblies = t;
                            else
                                throw new ArgumentException("Incorrect value of 'ignoreSystemAssemblies' parameter");
                            break;

                        case "output":
                            _outputFolder = value;
                            break;
                    }

                }
            }

            if (string.IsNullOrEmpty(_filePath))
                throw new Exception("'file' parameter is mandatory.");

            if (string.IsNullOrEmpty(_outputFolder))
                throw new Exception("'output' parameter is mandatory.");

        }

        #endregion
    }
}
