using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClrMDRUtil.Utils;

namespace ClrMDRIndex
{
    public class TypeFldInfo
    {
        #region fields
        int _loaded;
        string _dataPath;
        int[] _typeIds;
        int[] _fldTypeIds;
        int[] _fldNameIds;
        int[] _baseClasses; // TODO JRD
        int[][] _interfaces; // TODO JRD

        ClrElementKind[] _fldKinds;

        private bool IsLoaded => _loaded > 0;

        #endregion fields

        #region ctors/initialization

        public TypeFldInfo(string dataPath)
        {
            _dataPath = dataPath;
            _loaded = 0;
        }

        bool Load(out string error)
        {
            BinaryReader br = null;
            error = null;
            try
            {
                br = new BinaryReader(File.Open(_dataPath, FileMode.Open, FileAccess.Read));
                int count = br.ReadInt32();
                _typeIds = new int[count];
                _fldTypeIds = new int[count];
                _fldNameIds = new int[count];
                _fldKinds = new ClrElementKind[count];
                for (int i = 0; i < count; ++i)
                {
                    _typeIds[i] = br.ReadInt32();
                    _fldTypeIds[i] = br.ReadInt32();
                    _fldKinds[i] = (ClrElementKind)br.ReadInt32();
                    _fldNameIds[i] = br.ReadInt32();
                }
                Interlocked.Increment(ref _loaded);
                return true;
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                br?.Close();
            }
        }


        #endregion ctors/initialization

        #region queries

        public ValueTuple<int[],ClrElementKind[],int[]> GetTypeFieldInfo(int typeId, out string error)
        {
            error = null;
            try
            {
                if (!IsLoaded && !Load(out error)) return (null, null, null);
                int typeIdNdx = Array.BinarySearch(_typeIds, typeId);
                if (typeIdNdx < 0)
                {
                    error = Constants.InformationSymbolHeader + "Cannot find type id '" + typeId.ToString() + "' in TypeFldInfo data.";
                    return (null, null, null);
                }
                int first = typeIdNdx;
                while (first > 0 && _typeIds[first - 1] == typeId) --first;
                int lastNdx = _typeIds.Length;
                int end = typeIdNdx + 1;
                while (end < lastNdx && _typeIds[end] == typeId) ++end;
                int cnt = end - first;
                int[] types = new int[cnt];
                ClrElementKind[] kinds = new ClrElementKind[cnt];
                int[] nameIds = new int[cnt];
                int ndx = 0;
                for (int i = first; i < end; ++i)
                {
                    types[ndx] = _fldTypeIds[i];
                    kinds[ndx] = _fldKinds[i];
                    nameIds[ndx] = _fldNameIds[i];
                    ++ndx;
                }
                return (types, kinds, nameIds);
            }
            catch(Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (null,null,null);
            }
        }


        public ValueTuple<int[], int[]> GetTypesWithFieldType(int fldTypeId, out string error)
        {
            error = null;
            try
            {
                if (!IsLoaded && !Load(out error)) return (null, null);

                List<int> fldTypes = new List<int>(64);
                List<int> fldNames = new List<int>(64);
                int currentTypeId = Constants.InvalidIndex;
                for (int i = 0, icnt = _fldTypeIds.Length; i < icnt; ++i)
                {
                    if (_fldTypeIds[i] == fldTypeId && currentTypeId != _typeIds[i])
                    {
                        fldTypes.Add(_typeIds[i]);
                        fldNames.Add(_fldNameIds[i]);
                        currentTypeId = _typeIds[i];
                    }
                }
                return (fldTypes.ToArray(), fldNames.ToArray());
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return (null, null);
            }
        }

        #endregion queries

    }
}
