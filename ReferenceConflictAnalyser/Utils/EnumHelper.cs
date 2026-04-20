using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceConflictAnalyser.Utils
{
    /// <summary>
    /// Utilidades de reflexión para enumeraciones que usan el atributo
    /// <see cref="DescriptionAttribute"/> en sus valores.
    ///
    /// Se usa principalmente para obtener las etiquetas legibles en idioma natural
    /// de los valores de <see cref="ReferenceConflictAnalyser.DataStructures.Category"/> y
    /// <see cref="ReferenceConflictAnalyser.DataStructures.ExtraNodeProperty"/>, que se
    /// incorporan en el documento DGML generado por <see cref="ReferenceConflictAnalyser.GraphBuilder"/>.
    /// </summary>
    public class EnumHelper
    {

        /// <summary>
        /// Devuelve la descripción asociada al valor del enumerado mediante el atributo
        /// <see cref="DescriptionAttribute"/>. Si el valor no tiene el atributo, devuelve
        /// el nombre del valor como cadena (comportamiento de fallback).
        ///
        /// Ejemplo: <c>EnumHelper.GetDescription(Category.Missed)</c> devuelve
        /// <c>"Assembly is missed or failed to load"</c>.
        /// </summary>
        /// <typeparam name="T">Tipo del enumerado.</typeparam>
        /// <param name="value">Valor del enumerado cuya descripción se desea obtener.</param>
        /// <returns>
        ///   Cadena con el texto del <see cref="DescriptionAttribute"/>, o el nombre del valor
        ///   si no tiene el atributo.
        /// </returns>
        public static string GetDescription<T>(T value)
        {
            var type = typeof(T);
            var memInfo = type.GetMember(value.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Count() > 0
                ? ((DescriptionAttribute)attributes[0]).Description
                : value.ToString();
        }

        /// <summary>
        /// Devuelve un diccionario con todos los valores del enumerado <typeparamref name="T"/>
        /// mapeados a su descripción (<see cref="DescriptionAttribute"/> o nombre del valor).
        ///
        /// Se utiliza en <see cref="GraphBuilder.AddProperties"/> y
        /// <see cref="GraphBuilder.AddStyles"/> para iterar sobre todas las categorías o
        /// propiedades y generar los elementos DGML correspondientes.
        /// </summary>
        /// <typeparam name="T">Tipo del enumerado.</typeparam>
        /// <returns>
        ///   Diccionario donde cada clave es un valor del enumerado y su valor es la descripción
        ///   correspondiente.
        /// </returns>
        public static Dictionary<T, string> GetValuesWithDescriptions<T>()
        {
            return Enum.GetValues(typeof(T))
                .Cast<T>()
                .ToDictionary(x => x, x => GetDescription<T>(x));
        }
    }
}
