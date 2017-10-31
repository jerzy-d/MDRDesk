using GraphX.PCL.Common.Models;
using System.Collections.Generic;

namespace MDRDesk
{
    /* DataVertex is the data class for the vertices. It contains all custom vertex data specified by the user.
     * This class also must be derived from VertexBase that provides properties and methods mandatory for
     * correct GraphX operations.
     * Some of the useful VertexBase members are:
     *  - ID property that stores unique positive identfication number. Property must be filled by user.
     *  
     */

    public class DataVertex: VertexBase
    {
        /// <summary>
        /// Some string property for example purposes
        /// </summary>
        public string Text { get; set; }
 
        #region Calculated or static props

        public override string ToString()
        {
            return Text;
        }

        #endregion

        /// <summary>
        /// Default parameterless constructor for this class
        /// (required for YAXLib serialization)
        /// </summary>
        public DataVertex():this(string.Empty)
        {
        }

        public DataVertex(string text = "")
        {
            Text = text;
        }
    }

    public class DataVertexEqCmp : IEqualityComparer<DataVertex>
    {
        public bool Equals(DataVertex dv1, DataVertex dv2)
        {
            if (dv2 == null && dv1 == null)
                return true;
            else if (dv1 == null | dv2 == null)
                return false;
            return dv1.ID == dv2.ID;
        }

        public int GetHashCode(DataVertex dv)
        {
            return dv.ID.GetHashCode();
        }
    }

}
