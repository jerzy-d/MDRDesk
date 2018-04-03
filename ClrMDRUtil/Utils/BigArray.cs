using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClrMDRIndex
{
    //public class BigArray<T> where T : struct
    //{

    //    // 268435456 * 8 = 2147483648 (2GB)
    //    public const int BUFF_MAX_ITEM_COUNT = 4194304;
    //    const int MAX_BUF_COUNT = 64;
    //    public const int MAX_ITEM_COUNT = 268435456 - 64;

    //    List<T[]> _bufLst;
    //    int _bufCount;
    //    T[] _buf;

    //    public BigArray()
    //    {
    //        _bufLst = new List<T[]>(MAX_BUF_COUNT);
    //        _buf = new T[BUFF_MAX_ITEM_COUNT];
    //        _bufLst.Add(_buf);
    //        _bufCount = 0;
    //    }

    //    public void Add(T item)
    //    {
    //        if (_bufCount == BUFF_MAX_ITEM_COUNT)
    //        {
    //            _buf = new T[BUFF_MAX_ITEM_COUNT];
    //            _bufLst.Add(_buf);
    //            _bufCount = 0;
    //        }
    //        _buf[_bufCount++] = item;
    //    }

    //    public T[] GetArrayAndClean()
    //    {
    //        int count = 0;
    //        for(int i = 0, icnt = _bufLst.Count-1; i < icnt; ++i)
    //        {
    //            count += BUFF_MAX_ITEM_COUNT;
    //        }
    //        count += _bufCount;
    //        T[] ary = new T[count];
    //        int ndx = 0;
    //        for (int i = 0, icnt = _bufLst.Count - 1; i < icnt; ++i)
    //        {
    //            Array.Copy(_bufLst[i],0,ary,ndx,BUFF_MAX_ITEM_COUNT);
    //            _bufLst[i] = null;
    //            ndx += BUFF_MAX_ITEM_COUNT;
    //        }
    //        Array.Copy(_buf, 0, ary, ndx, _bufCount);
    //        _buf = null;
    //        return ary;
    //    }
    //}
}
