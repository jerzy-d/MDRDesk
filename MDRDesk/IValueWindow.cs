using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDRDesk
{
    interface IValueWindow
    {
        /// <summary>
        /// Unique id, across whole app.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// We have only few types of windows to dispaly instance values.
        /// </summary>
        ValueWindows.WndType WndType { get; }

        /// <summary>
        /// If locked we cannot replace window's content.
        /// </summary>
        bool Locked { get;  }
    }
}
