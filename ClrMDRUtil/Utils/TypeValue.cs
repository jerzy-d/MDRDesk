using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace ClrMDRIndex
{
    public class TypeValue
    {
        private int _typeId;
        private string _typeName;
        private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;

        private List<FieldValue> _fields;
        public List<FieldValue> Fields => _fields;

        public TypeValue(int typeId, string typeName, KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> category)
        {
            _typeId = typeId;
            _typeName = typeName;
            _category = category;
            _fields = null;
        }

    }

    public class FieldValue
    {
        private int _typeId;
        private string _typeName;
        private int _fieldIndex;
        private string _fieldName;
        private ClrType _clrType;
        public ClrType ClType => _clrType;
        private ClrInstanceField _instField;
        public ClrInstanceField InstField => _instField;

        private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;
        private List<string> _values;
        private List<FieldValue> _fields;
        public List<FieldValue> Fields => _fields;


    }

    public class TypeFilter
    {
        private int _typeId;
        private string _typeName;
        private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;

        private List<FieldFilter> _filters;
        public List<FieldFilter> Filters => _filters;

    }

    public class FieldFilter
    {
        private int _typeId;
        private string _typeName;
        private KeyValuePair<ValueExtractor.TypeCategory, ValueExtractor.TypeCategory> _category;
        private ClrType _clrType;
        public ClrType ClType => _clrType;
        private ClrInstanceField _instField;
        public ClrInstanceField InstField => _instField;
        private List<FieldFilter> _filters;
        public List<FieldFilter> Filters => _filters;

        public bool Accept(string value, bool accept)
        {
            return true;
        }
    }
}
