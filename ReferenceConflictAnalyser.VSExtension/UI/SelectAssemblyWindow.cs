//------------------------------------------------------------------------------
// <copyright file="SelectAssemblyWindow.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace ReferenceConflictAnalyser.VSExtension.UI
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// Ventana de herramientas (Tool Window) de Visual Studio que actúa como contenedor
    /// para el control de usuario <see cref="SelectAssemblyWindowControl"/>.
    ///
    /// En la arquitectura de extensiones de Visual Studio, una Tool Window tiene dos partes:
    ///   - El <b>frame</b>: implementado por el shell de VS; gestiona el ciclo de vida,
    ///     el anclaje (docking) y la visibilidad de la ventana.
    ///   - El <b>pane</b>: implementado por la extensión mediante <see cref="ToolWindowPane"/>;
    ///     contiene el contenido visual de la ventana.
    ///
    /// Esta clase representa el pane. Se registra como ventana flotante de instancia única
    /// mediante el atributo <see cref="ProvideToolWindowAttribute"/> en
    /// <see cref="ReferenceConflictAnalyserPackage"/>.
    ///
    /// Al crearse, instancia <see cref="SelectAssemblyWindowControl"/> y lo asigna como
    /// contenido (<see cref="ToolWindowPane.Content"/>). ToolWindowPane gestiona el ciclo
    /// de vida del control y llama a su Dispose cuando la ventana se cierra.
    /// </summary>
    [Guid("64f83bf2-678c-4b70-97e1-dc1f21dd29d1")]
    public class SelectAssemblyWindow : ToolWindowPane
    {
        /// <summary>
        /// Inicializa la ventana de herramientas estableciendo su título y creando el control
        /// WPF que se mostrará como contenido de la ventana.
        /// </summary>
        public SelectAssemblyWindow() : base(null)
        {
            this.Caption = "Select Assembly";

            // El control WPF es el contenido visual de la ventana.
            // ToolWindowPane llama a Dispose sobre Content cuando la ventana se destruye,
            // por lo que no es necesario gestionarlo manualmente.
            this.Content = new SelectAssemblyWindowControl(this);
           
        }
    }
}
