using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
	public class ClrtTypes
	{
        #region Fields/Properties

        public string[] Names; // sorted by StringComparison.Ordinal
		public string[] ReversedNames; // sorted by type class name followed by spacename
		public int[] ReversedNamesMap; // map of ReversedNames to Names



		public ulong[] MethodTables; // sorted
		public int[] MethodTablesMap; // map from MethodTables order to type (Names) order

		public int[][] FieldTypeIds;
		public int[][] FieldNameIds;

		public int[][] StaticFieldTypeIds;
		public int[][] StaticFieldNameIds;

		public ClrElementType[] ElementTypes;
		public int[] Bases;

		public int Count => Names.Length;

        private KeyValuePair<int, KeyValuePair<int, int>[]>[] _namespaces;

        #endregion Fields/Properties

        #region Ctrs/Initialization

        public ClrtTypes(string[] names, string[] reversedNames, int[] reversedNamesMap, 
                            ulong[] methodTables, int[] methodTablesMap, ClrElementType[] elementTypes, int[] bases,
                            int[][] fieldTypeIds, int[][] fieldNameIds, int[][] staticFieldTypeIds, int[][] staticFieldNameIds,
                            KeyValuePair<int, KeyValuePair<int, int>[]>[] namespaces)
        {
            Names = names;
	        ReversedNames = reversedNames;
	        ReversedNamesMap = reversedNamesMap;
            MethodTables = methodTables;
            MethodTablesMap = methodTablesMap;
			ElementTypes = elementTypes;
            Bases = bases;
			FieldTypeIds = fieldTypeIds;
			FieldNameIds = fieldNameIds;
			StaticFieldTypeIds = staticFieldTypeIds;
			StaticFieldNameIds = staticFieldNameIds;
            _namespaces = namespaces;
        }

        public ClrtTypes(int size)
		{
			Names = new string[size];
			ReversedNames = new string[size];
			ReversedNamesMap = new int[size];
			MethodTables = new ulong[size];
			MethodTablesMap = new int[size];
			ElementTypes = new ClrElementType[size];
			Bases = new int[size];
			FieldTypeIds = FieldNameIds = StaticFieldTypeIds = StaticFieldNameIds = null;
		}

		public void AddType(int ndx, string name, string reversedName, ClrElementType elem)
		{
			Names[ndx] = name;
			ReversedNames[ndx] = reversedName;
			ReversedNamesMap[ndx] = ndx;
			MethodTables[ndx] = Constants.InvalidAddress;
			ElementTypes[ndx] = elem;
			MethodTablesMap[ndx] = Constants.InvalidIndex;
			Bases[ndx] = Constants.InvalidIndex;
		}

        public void AddAdditionalInfos(int[] bases, ulong[] methods, int[] methdMap)
        {
            Bases = bases;
            MethodTables = methods;
            MethodTablesMap = methdMap;
            Array.Sort(ReversedNames, ReversedNamesMap, StringComparer.Ordinal);
        }
        public void GenerateNamespaceOrdering(StringIdDct ids)
        {
            SortedDictionary<int,List<KeyValuePair<int,int>>> dct = new SortedDictionary<int, List<KeyValuePair<int, int>>>();
            string[] splitter = new[] {Constants.NamespaceSepPadded};
            int nameId, nsId;
            for (int i = 0, icnt = ReversedNames.Length; i < icnt; ++i)
            {
                string[] items = ReversedNames[i].Split(splitter,StringSplitOptions.None);
                if (items.Length == 2)
                {
                    nameId = ids.JustGetId(items[0]);
                    nsId = ids.JustGetId(items[1]);
                }
                else
                {
                    nameId = ids.JustGetId(items[0]);
                    nsId = ids.JustGetId(string.Empty);

                }
                List<KeyValuePair<int, int>> lst;
                if (dct.TryGetValue(nsId, out lst))
                {

                    lst.Add(new KeyValuePair<int, int>(nameId,ReversedNamesMap[i]));
                }
                else
                {
                    dct.Add(nsId,new List<KeyValuePair<int, int>>() { new KeyValuePair<int, int>(nameId, ReversedNamesMap[i]) });
                }
            }

            KeyValuePair<int, KeyValuePair<int, int>[]>[] ns = new KeyValuePair<int, KeyValuePair<int, int>[]>[dct.Count];
            int ndx = 0;
            var cmp = new Utils.KVIntIntCmp();
            foreach (var kv in dct)
            {
                kv.Value.Sort(cmp);
                var ary = kv.Value.ToArray();
                ns[ndx++] = new KeyValuePair<int, KeyValuePair<int, int>[]>(
                    kv.Key,
                    ary
                    );
            }
            _namespaces = ns;
        }

        public void AddFieldInfos(int[][] fieldTypeIds, int[][] fieldNameIds, int[][] staticFieldTypeIds, int[][] staticFieldNameIds)
        {
            FieldTypeIds = fieldTypeIds;
            FieldNameIds = fieldNameIds;
            StaticFieldTypeIds = staticFieldTypeIds;
            StaticFieldNameIds = staticFieldNameIds;
        }

        public bool Dump(string path, out string error)
        {
            error = null;
            BinaryWriter br = null;
            StreamWriter sw = null;
            try
            {
                br = new BinaryWriter(File.Open(path, FileMode.Create));

                br.Write(Count);
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(Names[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(ReversedNames[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(ReversedNamesMap[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(MethodTables[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(MethodTablesMap[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write((int)ElementTypes[i]);
                }
                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    br.Write(Bases[i]);
                }

                for (int i = 0, icnt = Count; i < icnt; ++i)
                {
                    int cnt = FieldTypeIds[i].Length;
                    br.Write(cnt);
                    for (int j = 0; j < cnt; ++j)
                    {
                        br.Write(FieldTypeIds[i][j]);
                        br.Write(FieldNameIds[i][j]);
                    }
                    cnt = StaticFieldNameIds[i].Length;
                    br.Write(cnt);
                    for (int j = 0; j < cnt; ++j)
                    {
                        br.Write(StaticFieldTypeIds[i][j]);
                        br.Write(StaticFieldNameIds[i][j]);
                    }
                }

                int nsCnt = _namespaces.Length;
                br.Write(nsCnt);
                for (int i = 0; i < nsCnt; ++i)
                {
                    br.Write(_namespaces[i].Key);
                    br.Write(_namespaces[i].Value.Length);
                    for (int j = 0, jcnt = _namespaces[i].Value.Length; j < jcnt; ++j)
                    {
                        br.Write(_namespaces[i].Value[j].Key);
                        br.Write(_namespaces[i].Value[j].Value);
                    }
                }
            


    br.Close();
                br = null;

#if DEBUG
				path = path + ".txt";
				sw = new StreamWriter(path);
				sw.WriteLine(Count.ToString());
				for (int i = 0, icnt = Count; i < icnt; ++i)
				{
					sw.WriteLine(Utils.SmallIdHeader(i) + Names[i]);
				}
				sw.Close();
				sw = null;
#endif
                return true;
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return false;
            }
            finally
            {
                br?.Close();
                sw?.Close();
            }
        }

        public static ClrtTypes Load(string path, out string error)
        {
            error = null;
            BinaryReader br = null;
            try
            {
                br = new BinaryReader(File.Open(path, FileMode.Open));

                int count = br.ReadInt32();
                string[] typeNames = new string[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    typeNames[i] = br.ReadString();
                }

                string[] reversedNames = new string[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    reversedNames[i] = br.ReadString();
                }

                int[] reversedNamesMap = new int[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    reversedNamesMap[i] = br.ReadInt32();
                }


                ulong[] mthdTbls = new ulong[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    mthdTbls[i] = br.ReadUInt64();
                }

                int[] mthdTblsMap = new int[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    mthdTblsMap[i] = br.ReadInt32();
                }

                ClrElementType[] elems = new ClrElementType[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    elems[i] = (ClrElementType)br.ReadInt32();
                }
                int[] bases = new int[count];
                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    bases[i] = br.ReadInt32();
                }

                int[][] fldTypes = new int[count][];
                int[][] fldNames = new int[count][];
                int[][] statFldTypes = new int[count][];
                int[][] statFldNames = new int[count][];

                for (int i = 0, icnt = count; i < icnt; ++i)
                {
                    int cnt = br.ReadInt32();
                    if (cnt == 0)
                    {
                        fldTypes[i] = fldNames[i] = Utils.EmptyArray<int>.Value;
                    }
                    else
                    {
                        fldTypes[i] = new int[cnt];
                        fldNames[i] = new int[cnt];
                        for (int j = 0; j < cnt; ++j)
                        {
                            fldTypes[i][j] = br.ReadInt32();
                            fldNames[i][j] = br.ReadInt32();
                        }
                    }

                    cnt = br.ReadInt32();
                    if (cnt == 0)
                    {
                        statFldTypes[i] = statFldNames[i] = Utils.EmptyArray<int>.Value;
                    }
                    else
                    {
                        statFldTypes[i] = new int[cnt];
                        statFldNames[i] = new int[cnt];
                        for (int j = 0; j < cnt; ++j)
                        {
                            statFldTypes[i][j] = br.ReadInt32();
                            statFldNames[i][j] = br.ReadInt32();
                        }
                    }
                }


                //int nsCnt = _namespaces.Length;
                //br.Write(nsCnt);
                //for (int i = 0; i < nsCnt; ++i)
                //{
                //    br.Write(_namespaces[i].Key);
                //    br.Write(_namespaces[i].Value.Length);
                //    for (int j = 0, jcnt = _namespaces[i].Value.Length; j < jcnt; ++j)
                //    {
                //        br.Write(_namespaces[i].Value[j].Key);
                //        br.Write(_namespaces[i].Value[j].Value);
                //    }
                //}
                int nsCnt = br.ReadInt32();
                var namespaces = new KeyValuePair<int, KeyValuePair<int, int>[]>[nsCnt];
                for (int i = 0; i < nsCnt; ++i)
                {
                    var nsId = br.ReadInt32();
                    var cnt = br.ReadInt32();
                    KeyValuePair < int, int>[] classes = new KeyValuePair<int, int>[cnt];
                    for (int j = 0; j < cnt; ++j)
                    {
                        classes[j] = new KeyValuePair<int, int>(
                            br.ReadInt32(),
                            br.ReadInt32()
                            );
                    }
                    namespaces[i] = new KeyValuePair<int, KeyValuePair<int, int>[]>(nsId,classes);
                }

                return new ClrtTypes(typeNames, reversedNames, reversedNamesMap, mthdTbls, mthdTblsMap, elems, bases, fldTypes, fldNames, statFldTypes, statFldNames,namespaces);
            }
            catch (Exception ex)
            {
                error = Utils.GetExceptionErrorString(ex);
                return null;
            }
            finally
            {
                br?.Close();
            }
        }

        #endregion Ctrs/Initialization

		public static bool HasInternalAddresses(ClrType clrType)
		{
			return clrType.IsValueClass;
		}

        public int GetTypeId(string name)
		{
			int id = Array.BinarySearch(Names, name, StringComparer.Ordinal);
			return id < 0 ? Constants.InvalidIndex : id;
		}

		public int[] GetTypeIds(string prefix)
		{
			List<int> lst = new List<int>();
			int id = Array.BinarySearch(Names, prefix, StringComparer.Ordinal);
			if (id < 0) id = ~id;
			for (int i = id; i >= 0 && i < Names.Length; --i)
			{
				if (Names[i].StartsWith(prefix,StringComparison.Ordinal)) lst.Add(i);
				else break;
			}
			for (int i = id+1; i < Names.Length; ++i)
			{
				if (Names[i].StartsWith(prefix, StringComparison.Ordinal)) lst.Add(i);
				else break;
			}
			lst.Sort();
			return lst.Count > 0 ? lst.ToArray() : Utils.EmptyArray<int>.Value;
		}

		public int GetTypeId(ulong methodTbl)
		{
			var pos = Array.BinarySearch(MethodTables, methodTbl);
			if (pos < 0) return Constants.InvalidIndex;
			return MethodTablesMap[pos];
		}

		public string GetName(int id)
		{
			if (id < 0 || id >= Names.Length) return Constants.NullName;
			return Names[id];
		}

		public bool IsArray(int typeId)
		{
			if (typeId < 0 || typeId >= ElementTypes.Length) return false;
			return ElementTypes[typeId] == ClrElementType.SZArray || ElementTypes[typeId] == ClrElementType.Array;
		}

		public ulong GetMethodTable(int id)
		{
			return MethodTables[MethodTablesMap[id]];
		}

		public int GetBaseId(int id)
		{
			return Bases[id];
		}

        public int[] GetSameBaseTypeIds(int id)
        {
            var baseId = Bases[id];
            if (baseId == Constants.InvalidIndex) return Utils.EmptyArray<int>.Value;
            List<int> lst = new List<int>(128);
            for (int i = 0, icnt = Bases.Length; i < icnt; ++i)
            {
                if (Bases[i] == baseId) lst.Add(i);
            }
            return lst.ToArray();
        }

        public ClrElementType GetElementType(int id)
		{
			if (id < 0 || id >= Names.Length) return ClrElementType.Unknown;
			return ElementTypes[id];
		}

	    public KeyValuePair<string, KeyValuePair<string,int>[]>[] GetNamespaceDisplay(string[] idStrs)
	    {
            // (namespace, (type name, type id))
            KeyValuePair<string, KeyValuePair<string, int>[]>[] result = new KeyValuePair<string, KeyValuePair<string, int>[]>[_namespaces.Length];

            for (int i = 0, icnt = _namespaces.Length; i < icnt; ++i)
	        {
                KeyValuePair<string, int>[] classInfos = new KeyValuePair<string, int>[_namespaces[i].Value.Length];
	            for (int j = 0, jcnt = _namespaces[i].Value.Length; j < jcnt; ++j)
	            {
                    classInfos[j] = new KeyValuePair<string, int>(
                        idStrs[_namespaces[i].Value[j].Key],
                        _namespaces[i].Value[j].Value
                        );

                }
                result[i] = new KeyValuePair<string, KeyValuePair<string, int>[]>(
                    idStrs[_namespaces[i].Key],
                    classInfos
                    );
	        }
            Array.Sort(result,new Utils.KvStrKvStrInt());
            Utils.KvStrInt cmpr = new Utils.KvStrInt();
            for (int i = 0, icnt = result.Length; i < icnt; ++i)
	        {
                Array.Sort(result[i].Value,cmpr);
            }
	        return result;
	    }
    }

	public class ClrtType
	{
		public string Name;
		public ulong MthdTbl;
		public ClrElementType Element;
		public string BaseName;
        public string[] FieldTypeNames;
        public ulong[] FieldMts;
        public int[] FieldNameIds;
        public string[] StaticFieldTypeNames;
        public ulong[] StaticFieldMts;
        public int[] StaticFieldNameIds;
		public int[] InterfaceNameIds;

		public ClrtType(string name, ulong mthdTbl, ClrElementType elem, string baseName)
		{
			Name = name;
			MthdTbl = mthdTbl;
			Element = elem;
			BaseName = baseName;
			FieldTypeNames = StaticFieldTypeNames = null;
			FieldNameIds = StaticFieldNameIds = InterfaceNameIds = Utils.EmptyArray<int>.Value;
		    FieldMts = StaticFieldMts = Utils.EmptyArray<ulong>.Value;
		}

	    public void AddFieldInfo(string[] fieldTypeNames, ulong[] fieldMts, int[] fieldNameIds, string[] staticFieldTypeNames, ulong[] staticFieldMts,
	        int[] staticFieldNameIds)
	    {
	        FieldTypeNames = fieldTypeNames;
	        FieldMts = fieldMts;
	        FieldNameIds = fieldNameIds;
	        StaticFieldTypeNames = staticFieldTypeNames;
	        StaticFieldMts = staticFieldMts;
	        StaticFieldNameIds = staticFieldNameIds;
	    }

		public bool HasFieldInfo()
		{
			return FieldTypeNames != null;
		}

		public bool IsArray()
		{
			return Element == ClrElementType.Array;
		}

	public static string GetKey(string name, ulong mthdTbl)
		{
			return name + "!" + mthdTbl;
		}

	}
}
