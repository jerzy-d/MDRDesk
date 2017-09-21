using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    public class TypeInterfaces
    {
        #region fields/properties

        string[] _typeNames;
        string[][] _interfaces;
        string[] _interfaceNames;
        string[][] _types;
        KeyValuePair<string, string>[] _notFoundInterfaces;

        #endregion fields/properties

        #region ctors/initialization

        private TypeInterfaces()
        {

        }

        public static TypeInterfaces Load(string path, out string error)
        {
            error = null;
            StreamReader sr = null;
            try
            {
                StringCache cache = new StringCache(StringComparer.Ordinal);
                 
                var typeInterfaces = new TypeInterfaces();
                sr = new StreamReader(path);
                int cnt = Int32.Parse(sr.ReadLine());
                typeInterfaces._typeNames = new string[cnt];
                typeInterfaces._interfaces = new string[cnt][];
                for (int i = 0; i < cnt; ++i)
                {
                    var entry = sr.ReadLine();
                    var items = entry.Split(Constants.HeavyGreekCross);
                    typeInterfaces._typeNames[i] = cache.GetCachedString(items[0]);
                    typeInterfaces._interfaces[i] = new string[items.Length - 1];
                    for (int j = 1, jcnt = items.Length; j < jcnt; ++j)
                    {
                        typeInterfaces._interfaces[i][j - 1] = cache.GetCachedString(items[j]);
                    }
                }
                cnt = Int32.Parse(sr.ReadLine());
                typeInterfaces._interfaceNames = new string[cnt];
                typeInterfaces._types = new string[cnt][];
                for (int i = 0; i < cnt; ++i)
                {
                    var entry = sr.ReadLine();
                    var items = entry.Split(Constants.HeavyGreekCross);
                    typeInterfaces._interfaceNames[i] = cache.GetCachedString(items[0]);
                    typeInterfaces._types[i] = new string[items.Length - 1];
                    for (int j = 1, jcnt = items.Length; j < jcnt; ++j)
                    {
                        typeInterfaces._types[i][j - 1] = cache.GetCachedString(items[j]);
                    }
                }
                cnt = Int32.Parse(sr.ReadLine());
                typeInterfaces._notFoundInterfaces = new KeyValuePair<string, string>[cnt];
                for (int i = 0; i < cnt; ++i)
                {
                    var entry = sr.ReadLine();
                    var items = entry.Split(Constants.HeavyGreekCross);
                    typeInterfaces._notFoundInterfaces[i] = new KeyValuePair<string, string>(items[0],items[1]);
                }
                return typeInterfaces;
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                sr?.Close();
            }
        }

        #endregion ctors/initialization

        #region queries

        public string[] GetInterfaces(string typeName)
        {
            var ndx = Array.BinarySearch(_typeNames, typeName, StringComparer.Ordinal);
            return ndx < 0
                ? Utils.EmptyArray<string>.Value
                : _interfaces[ndx];
        }

        public string[] GetTypes(string interfaceName)
        {
            var ndx = Array.BinarySearch(_interfaceNames, interfaceName, StringComparer.Ordinal);
            return ndx < 0
                ? Utils.EmptyArray<string>.Value
                : _types[ndx];
        }

        #endregion queries

    }
}
