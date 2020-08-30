using System;

namespace Swiddler.Serialization.Pcap
{
    public static class ReverseByteOrderExtension
    {          
        #region Extenstion method
        public static UInt32 ReverseByteOrder(this UInt32 value,bool reverseByteOrder)
        {
            if (!reverseByteOrder)
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value); 
                Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }

        public static Int32 ReverseByteOrder(this Int32 value, bool reverseByteOrder)
        {
            if (!reverseByteOrder)
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                return BitConverter.ToInt32(bytes, 0);
            }
        }

        public static UInt16 ReverseByteOrder(this UInt16 value, bool reverseByteOrder)
        {
            if (!reverseByteOrder)
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                return BitConverter.ToUInt16(bytes, 0);
            }
        }

        public static Int16 ReverseByteOrder(this Int16 value, bool reverseByteOrder)
        {
            if (!reverseByteOrder)                
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                return BitConverter.ToInt16(bytes, 0);
            }
        }

        public static UInt64 ReverseByteOrder(this UInt64 value, bool reverseByteOrder)
        {
            if (!reverseByteOrder)
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                return BitConverter.ToUInt64(bytes, 0);
            }
        }

        public static Int64 ReverseByteOrder(this Int64 value, bool reverseByteOrder)
        {
            if (!reverseByteOrder)
                return value;
            else
            {
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Reverse(bytes);
                return BitConverter.ToInt64(bytes, 0);
            }
        }
        #endregion
    }
}
