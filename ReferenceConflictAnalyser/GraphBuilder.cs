using ReferenceConflictAnalyser.DataStructures;
using ReferenceConflictAnalyser.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ReferenceConflictAnalyser
{
    /// <summary>
    /// Convierte un <see cref="ReferenceList"/> (resultado del análisis) en un documento XML
    /// con formato DGML (Directed Graph Markup Language), el formato nativo de Visual Studio
    /// para visualizar grafos dirigidos.
    ///
    /// El documento generado incluye:
    ///   - <b>Nodes</b>: un nodo por ensamblado, con icono, categoría y propiedades extra.
    ///     Los ensamblados con errores o conflictos llevan además un nodo de comentario adjunto.
    ///     Los ensamblados no utilizados se agrupan bajo un nodo contenedor colapsable.
    ///   - <b>Links</b>: aristas dirigidas entre ensamblados (Source → Target) con la versión
    ///     referenciada como etiqueta, más aristas de comentario y de agrupación.
    ///   - <b>Categories</b>: definición de las categorías visuales usadas en los nodos.
    ///   - <b>Styles</b>: reglas de estilo (colores de fondo, bordes, grosor) por categoría.
    ///   - <b>Properties</b>: metadatos adicionales visibles en el panel de propiedades de VS.
    /// </summary>
    public class GraphBuilder
    {

        /// <summary>
        /// Construye el documento DGML completo a partir del grafo de dependencias.
        /// </summary>
        /// <param name="referenceList">
        ///   Resultado del análisis producido por <see cref="ReferenceAnalyser"/>.
        /// </param>
        /// <returns>
        ///   Documento XML <see cref="XmlDocument"/> listo para serializar como archivo .dgml.
        /// </returns>
        public XmlDocument BuildDgml(ReferenceList referenceList)
        {
            _referenceList = referenceList;

            _doc = new XmlDocument();

            var root = AddRootElement();
            AddNodes(root);
            AddLinks(root);
            AddCategories(root);
            AddStyles(root);
            AddProperties(root);

            return _doc;
        }

        #region private members


        /// <summary>Namespace XML estándar del formato DGML de Visual Studio 2009+.</summary>
        private const string XmlNamespace = "http://schemas.microsoft.com/vs/2009/dgml";

        /// <summary>
        /// Mapeo de cada categoría de ensamblado a su color de fondo en el grafo DGML.
        /// Permite identificar visualmente el estado de cada nodo de un vistazo:
        ///   - Verde claro: punto de entrada del análisis.
        ///   - MintCream: referencia normal sin problemas.
        ///   - LightSalmon: conflicto de versión sin resolver.
        ///   - Coral: otro conflicto (ej. arquitectura).
        ///   - Khaki: conflicto resuelto por bindingRedirect.
        ///   - Crimson: ensamblado faltante o que no pudo cargarse.
        ///   - White: nodo de comentario/detalle.
        ///   - Gray: ensamblado presente en disco pero no referenciado.
        /// </summary>
        private readonly Dictionary<Category, Color> _categories = new Dictionary<Category, Color>()
        {
            { Category.EntryPoint, Color.LightGreen },
            { Category.Normal , Color.MintCream },
            { Category.VersionsConflicted, Color.LightSalmon },
            { Category.OtherConflict, Color.Coral },
            { Category.VersionsConflictResolved, Color.Khaki },
            { Category.Missed, Color.Crimson },
            { Category.Comment, Color.White },
            { Category.UnusedAssembly, Color.Gray }
        };
        private Color PlatformTargetMismatchBorder = Color.DarkRed;
        private ReferenceList _referenceList;
        private XmlDocument _doc;

        /// <summary>ID del nodo contenedor que agrupa los ensamblados no utilizados.</summary>
        private const string UnusedGroupNodeId = "UnusedGroupNodeId";

        /// <summary>ID del nodo de comentario explicativo dentro del grupo de no utilizados.</summary>
        private const string UnusedGroupNodeCommentId = "UnusedGroupNodeCommentId";

        /// <summary>
        /// Crea y agrega el elemento raíz &lt;DirectedGraph&gt; con la configuración de layout.
        /// Se usa el algoritmo Sugiyama (jerarquía de capas de arriba hacia abajo) para
        /// que el ensamblado raíz quede en la parte inferior y sus dependencias arriba.
        /// </summary>
        /// <returns>El nodo XML raíz recién agregado al documento.</returns>
        private XmlNode AddRootElement()
        {
            var root = _doc.AppendChild(CreateXmlElement("DirectedGraph", new Dictionary<string, string>
            {
                { "GraphDirection", "BottomToTop"},
                { "Layout", "Sugiyama"}
            }));
            return root;
        }

        /// <summary>
        /// Crea el elemento &lt;Nodes&gt; con un hijo &lt;Node&gt; por cada ensamblado del grafo.
        ///
        /// Para cada ensamblado:
        ///   - Genera un nodo con Id (nombre en minúsculas), Label, Category e icono de ensamblado.
        ///   - Si el ensamblado tiene arquitectura de procesador conocida, agrega la propiedad
        ///     <see cref="ExtraNodeProperty.ProcessorArchitecture"/>.
        ///   - Si tiene errores o causas de carga, agrega un nodo de comentario adicional.
        ///
        /// Si hay ensamblados no utilizados, agrega un nodo grupo colapsable y su comentario.
        /// </summary>
        /// <param name="parent">Nodo XML padre (&lt;DirectedGraph&gt;).</param>
        private void AddNodes(XmlNode parent)
        {
            var nodesElement = parent.AppendChild(_doc.CreateElement("Nodes", XmlNamespace));
            foreach (var referencedAssembly in _referenceList.Assemblies)
            {
                var nodeId = referencedAssembly.Name.ToLower();

                var attributes = new Dictionary<string, string>
                {
                    { "Id", nodeId},
                    { "Label", referencedAssembly.Name},
                    { "Category", referencedAssembly.Category.ToString()},
                    { "Icon", "CodeSchema_Assembly"}
                };

                if (referencedAssembly.ProcessorArchitecture != ProcessorArchitecture.None)
                {
                    attributes.Add(ExtraNodeProperty.ProcessorArchitecture.ToString(), referencedAssembly.ProcessorArchitecture.ToString());
                }
                nodesElement.AppendChild(CreateXmlElement("Node", attributes));

                // Agregar nodo de comentario si el ensamblado tiene información de error o advertencia.
                if (HasCommentNode(referencedAssembly))
                {
                    nodesElement.AppendChild(CreateXmlElement("Node", new Dictionary<string, string>
                    {
                        { "Id", GetCommentNodeId(nodeId)},
                        { "Label", BuildComment(referencedAssembly)},
                        { "Category", Category.Comment.ToString()}
                    }));
                }
            }

            // Agregar nodo contenedor para los ensamblados no utilizados (grupo expandido por defecto).
            if (_referenceList.Assemblies.Any(x => x.Category == Category.UnusedAssembly))
            {
                nodesElement.AppendChild(CreateXmlElement("Node", new Dictionary<string, string>
                    {
                        { "Id", UnusedGroupNodeId },
                        { "Label", "Unused assemblies?" },
                        { "Group", "Expanded" }
                    }));
                nodesElement.AppendChild(CreateXmlElement("Node", new Dictionary<string, string>
                    {
                        { "Id", UnusedGroupNodeCommentId },
                        { "Label", "These assemblies are not referended by any assembly from the graph explicitly. However, they can be loaded in runtime by Assembly and AppDomain methods." },
                        { "Category", Category.Comment.ToString() }
                    }));
            }
        }

        /// <summary>
        /// Construye el texto del nodo de comentario para un ensamblado con errores o advertencias.
        /// Incluye el mensaje de excepción, el tipo de excepción y las causas posibles de error.
        /// </summary>
        /// <param name="referencedAssembly">Ensamblado del que se generará el comentario.</param>
        /// <returns>Cadena multilínea con la información del error.</returns>
        private string BuildComment(ReferencedAssembly referencedAssembly)
        {
            var comment = new StringBuilder();

            if (referencedAssembly.LoadingError != null)
            {
                comment.AppendLine($"Error message: {referencedAssembly.LoadingError.Message}");
                comment.AppendLine($"Error type: {referencedAssembly.LoadingError.GetType().Name}.");
            }

            if (referencedAssembly.PossibleLoadingErrorCauses != null && referencedAssembly.PossibleLoadingErrorCauses.Any())
            {
                comment.AppendLine($"Details: {string.Join("; ", referencedAssembly.PossibleLoadingErrorCauses)}");
            }

            return comment.ToString();
        }

        /// <summary>
        /// Determina si un ensamblado necesita un nodo de comentario auxiliar.
        /// Es necesario cuando hubo un error de carga o existen causas de error registradas.
        /// </summary>
        /// <param name="referencedAssembly">Ensamblado a evaluar.</param>
        /// <returns>true si se debe crear un nodo de comentario; false en caso contrario.</returns>
        private bool HasCommentNode(ReferencedAssembly referencedAssembly)
        {
            return referencedAssembly.LoadingError != null || referencedAssembly.PossibleLoadingErrorCauses.Any();
        }

        /// <summary>
        /// Genera el identificador único del nodo de comentario asociado a un nodo de ensamblado.
        /// El convenio es añadir el sufijo "..comment" al Id del nodo padre.
        /// </summary>
        /// <param name="assemblyNodeId">Id del nodo de ensamblado (en minúsculas).</param>
        /// <returns>Id del nodo de comentario correspondiente.</returns>
        private string GetCommentNodeId(string assemblyNodeId)
        {
            return $"{assemblyNodeId}..comment";
        }

        /// <summary>
        /// Crea el elemento &lt;Links&gt; con todas las aristas del grafo:
        ///   1. Aristas de dependencia entre ensamblados (Source → Target), con la versión
        ///      referenciada como etiqueta y los nombres completos como propiedades extra.
        ///   2. Aristas de comentario: unen cada ensamblado con errores a su nodo de comentario.
        ///   3. Aristas de agrupación: enlazan el nodo contenedor "Unused assemblies?" con cada
        ///      ensamblado no utilizado y con el comentario explicativo del grupo.
        /// </summary>
        /// <param name="parent">Nodo XML padre (&lt;DirectedGraph&gt;).</param>
        private void AddLinks(XmlNode parent)
        {
            var linksElement = parent.AppendChild(_doc.CreateElement("Links", XmlNamespace));

            // Aristas de dependencia entre ensamblados.
            foreach (var reference in _referenceList.References)
                linksElement.AppendChild(CreateXmlElement("Link", new Dictionary<string, string>
                {
                    { "Source", reference.Assembly.Name.ToLower()},
                    { "Target", reference.ReferencedAssembly.Name.ToLower()},
                    { "Label", reference.ReferencedAssembly.Version.ToString()},
                    { ExtraNodeProperty.SourceNodeDetails.ToString(), reference.Assembly.FullName },
                    { ExtraNodeProperty.TargetNodeDetails.ToString(), reference.ReferencedAssembly.FullName }
                }));

            // Aristas de ensamblado → nodo de comentario.
            var assembliesWithComments = _referenceList.Assemblies.Where(HasCommentNode);
            foreach (var referencedAssembly in assembliesWithComments)
            {
                var nodeId = referencedAssembly.Name.ToLower();
                linksElement.AppendChild(CreateXmlElement("Link", new Dictionary<string, string>
                {
                    { "Source", nodeId},
                    { "Target", GetCommentNodeId(nodeId)}
                }));
            }

            // Aristas de agrupación para el contenedor de ensamblados no utilizados.
            var unusedAssemblies = _referenceList.Assemblies.Where(x => x.Category == Category.UnusedAssembly);
            if (unusedAssemblies.Any())
            {
                // Arista hacia el comentario explicativo del grupo.
                linksElement.AppendChild(CreateXmlElement("Link", new Dictionary<string, string>
                {
                    { "Source", UnusedGroupNodeId},
                    { "Target", UnusedGroupNodeCommentId}
                }));

                foreach (var assembly in unusedAssemblies)
                {
                    // Arista "Contains" para colocar el nodo dentro del grupo colapsable.
                    linksElement.AppendChild(CreateXmlElement("Link", new Dictionary<string, string>
                    {
                        { "Source", UnusedGroupNodeId},
                        { "Target", assembly.Name.ToLower()},
                        { "Category", "Contains" }
                    }));

                    // Si el ensamblado no utilizado también tiene comentario, agruparlo también.
                    if (HasCommentNode(assembly))
                    {
                        linksElement.AppendChild(CreateXmlElement("Link", new Dictionary<string, string>
                        {
                                { "Source", UnusedGroupNodeId},
                                { "Target", GetCommentNodeId(assembly.Name.ToLower())},
                                { "Category", "Contains" }
                        }));
                    }
                }
            }
        }

        /// <summary>
        /// Crea el elemento &lt;Categories&gt; definiendo cada categoría con su Id y etiqueta
        /// descriptiva (tomada del atributo [Description] del enum <see cref="Category"/>).
        /// Visual Studio usa estas definiciones para aplicar los estilos correspondientes.
        /// </summary>
        /// <param name="parent">Nodo XML padre (&lt;DirectedGraph&gt;).</param>
        private void AddCategories(XmlNode parent)
        {
            var categoriesElement = parent.AppendChild(_doc.CreateElement("Categories", XmlNamespace));
            foreach (var category in _categories)
                categoriesElement.AppendChild(CreateXmlElement("Category", new Dictionary<string, string>
                {
                    { "Id", category.Key.ToString() },
                    { "Label", EnumHelper.GetDescription(category.Key)}
                }));
        }

        /// <summary>
        /// Crea el elemento &lt;Properties&gt; registrando las propiedades extra de los nodos
        /// (definidas en <see cref="ExtraNodeProperty"/>): detalles del nodo origen, detalles del
        /// nodo destino y arquitectura de procesador. Estas propiedades son visibles en el panel
        /// "Properties" de Visual Studio al seleccionar un nodo o arista del grafo.
        /// </summary>
        /// <param name="parent">Nodo XML padre (&lt;DirectedGraph&gt;).</param>
        private void AddProperties(XmlNode parent)
        {
            var propertiesElement = parent.AppendChild(_doc.CreateElement("Properties", XmlNamespace));

            var properties = EnumHelper.GetValuesWithDescriptions<ExtraNodeProperty>();
            foreach(var property in properties)
            {
                propertiesElement.AppendChild(CreateXmlElement("Property", new Dictionary<string, string>
                {
                    { "Id", property.Key.ToString() },
                    { "DataType", "System.String" },
                    { "Label", property.Value }
                }));
            }
        }

        /// <summary>
        /// Crea el elemento &lt;Styles&gt; con las reglas visuales del grafo:
        ///   - Un estilo de nodo por cada categoría, con el color de fondo definido en
        ///     <see cref="_categories"/>. Los nodos de comentario usan además ancho máximo,
        ///     radio de esquinas y color de texto diferenciados.
        ///   - Un estilo de arista para las conexiones hacia nodos conflictivos (borde grueso salmon).
        ///   - Un estilo de arista para las conexiones hacia nodos faltantes (borde grueso crimson).
        ///   - Un estilo de arista para las conexiones hacia nodos de comentario (línea discontinua).
        /// </summary>
        /// <param name="parent">Nodo XML padre (&lt;DirectedGraph&gt;).</param>
        private void AddStyles(XmlNode parent)
        {
            var stylesElement = parent.AppendChild(_doc.CreateElement("Styles", XmlNamespace));
            foreach (var category in _categories)
            {
                var properties = new Dictionary<string, string>
                {
                     { "Background", ColorTranslator.ToHtml(category.Value) }
                };

                // Los nodos de comentario tienen un estilo diferenciado para no confundirlos
                // con nodos de ensamblado: texto gris, ancho limitado y esquinas redondeadas.
                if (category.Key == Category.Comment)
                {
                    properties.Add("MaxWidth", "300");
                    properties.Add("NodeRadius", "15");
                    properties.Add("Foreground", ColorTranslator.ToHtml(Color.Gray));
                }
                    

                stylesElement.AppendChild(CreateStyleElement("Node", 
                    EnumHelper.GetDescription(category.Key), 
                    $"HasCategory('{category.Key}')", 
                    properties
                    ));
            }

            // Arista gruesa hacia ensamblados con conflicto de versión sin resolver.
            stylesElement.AppendChild(CreateStyleElement("Link",
                      "Link to conflicted reference",
                      $"Target.HasCategory('{Category.VersionsConflicted}')",
                      new Dictionary<string, string>
                      {
                            { "Stroke", ColorTranslator.ToHtml(_categories[Category.VersionsConflicted]) },
                            { "StrokeThickness", "3" }
                      }));

            // Arista gruesa hacia ensamblados faltantes.
            stylesElement.AppendChild(CreateStyleElement("Link",
                      "Link to missed reference",
                      $"Target.HasCategory('{Category.Missed}')",
                      new Dictionary<string, string>
                      {
                            { "Stroke", ColorTranslator.ToHtml(_categories[Category.Missed]) },
                            { "StrokeThickness", "3" }
                      }));

            // Arista discontinua hacia nodos de comentario, para distinguirla de las dependencias reales.
            stylesElement.AppendChild(CreateStyleElement("Link",
                      "Link to detailed information",
                      $"Target.HasCategory('{Category.Comment}')",
                      new Dictionary<string, string>
                      {
                          { "StrokeDashArray", "2 2" }
                      }));

        }

        /// <summary>
        /// Crea un elemento XML con el nombre y namespace DGML indicados, y agrega los atributos
        /// proporcionados en el diccionario. Es el método de construcción de elementos genérico
        /// usado por todos los métodos Add* de esta clase.
        /// </summary>
        /// <param name="elementName">Nombre del elemento XML (p.ej. "Node", "Link", "Style").</param>
        /// <param name="attributes">Diccionario nombre→valor de los atributos XML del elemento.</param>
        /// <returns>El elemento XML creado (aún no agregado al documento).</returns>
        private XmlElement CreateXmlElement(string elementName, Dictionary<string, string> attributes)
        {
            var elem = _doc.CreateElement(elementName, XmlNamespace);
            foreach (var attibute in attributes)
                elem.Attributes.Append(CreateXmlAtribute(attibute.Key, attibute.Value));
            return elem;
        }

        /// <summary>
        /// Crea un atributo XML con el nombre y valor indicados.
        /// </summary>
        /// <param name="name">Nombre del atributo.</param>
        /// <param name="value">Valor del atributo.</param>
        /// <returns>El atributo XML creado.</returns>
        private XmlAttribute CreateXmlAtribute(string name, string value)
        {
            var attribute = _doc.CreateAttribute(name);
            attribute.Value = value;
            return attribute;
        }

        /// <summary>
        /// Crea un elemento &lt;Style&gt; DGML completo con su condición de activación y
        /// sus setters de propiedades visuales.
        ///
        /// Un &lt;Style&gt; DGML tiene la estructura:
        /// <code>
        /// &lt;Style TargetType="Node" GroupLabel="..." &gt;
        ///   &lt;Condition Expression="HasCategory('X')" /&gt;
        ///   &lt;Setter Property="Background" Value="#RRGGBB" /&gt;
        /// &lt;/Style&gt;
        /// </code>
        /// </summary>
        /// <param name="targetType">Tipo de elemento al que aplica el estilo ("Node" o "Link").</param>
        /// <param name="groupLabel">Etiqueta descriptiva del grupo de estilo.</param>
        /// <param name="condition">Expresión DGML que activa el estilo (p.ej. "HasCategory('X')").</param>
        /// <param name="properties">Diccionario propiedad→valor de los setters visuales.</param>
        /// <returns>El elemento &lt;Style&gt; XML listo para ser agregado al documento.</returns>
        private XmlElement CreateStyleElement(string targetType, string groupLabel, string condition, Dictionary<string, string> properties)
        {
            var styleElement = _doc.CreateElement("Style", XmlNamespace);
            styleElement.Attributes.Append(CreateXmlAtribute("TargetType", targetType));
            styleElement.Attributes.Append(CreateXmlAtribute("GroupLabel", groupLabel));

            var conditionElement = _doc.CreateElement("Condition", XmlNamespace);
            conditionElement.Attributes.Append(CreateXmlAtribute("Expression", condition));
            styleElement.AppendChild(conditionElement);

            foreach (var property in properties)
                styleElement.AppendChild(CreateXmlElement("Setter", new Dictionary<string, string>
                {
                     { "Property", property.Key },
                     { "Value", property.Value }
                }));

            return styleElement;
        }

        #endregion
    }
}
