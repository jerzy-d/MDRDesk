using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClrMDRUtil.Utils;

namespace ClrMDRIndex
{
    class TypeFldInfo
    {
        string _dataPath;
        int[] _typeIds;
        int[] _fldTypeIds;
        int[] _fldNameIds;
        ClrElementKind[] _fldKinds;
        StringIdDct _strIds;

        public TypeFldInfo(string dataPath, StringIdDct strIds)
        {
            _dataPath = dataPath;
            _strIds = strIds;
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
    }
}
