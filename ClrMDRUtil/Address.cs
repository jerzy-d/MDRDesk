using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#if X86PLATFORM
using uaddr = System.UInt32;
#else
using uaddr = System.UInt64;
#endif

namespace ClrMDRIndex
{
    public struct Address
    {
        private uaddr _address;

        // As we are using implicit conversions we can keep the constructor private
        private Address(ulong value)
        {
            this._address = (uaddr)value;
        }

        /// <summary>
        /// Implicitly converts a <see cref="System.32"/> to a Record.
        /// </summary>
        /// <param name="value">The <see cref="System.Int32"/> to convert.</param>
        /// <returns>A new Address with the specified value.</returns>
        public static implicit operator Address(ulong value)
        {
            return new Address(value);
        }
        /// <summary>
        /// Implicitly converts a Address to a <see cref="System.UInt64"/>.
        /// </summary>
        /// <param name="address">The Address to convert.</param>
        /// <returns>
        /// A <see cref="System.UInt64"/> that is the specified Address's value.
        /// </returns>
        public static implicit operator ulong(Address record)
        {
            return record._address;
        }
         

    }
}
