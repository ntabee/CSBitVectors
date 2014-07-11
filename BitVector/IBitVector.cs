using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitVectors
{
    interface IBitVector<TSelf> where TSelf: IBitVector<TSelf>
    {
        ulong size();
        ulong size(bool b);
        bool get(ulong i);
        ulong rank(ulong i, bool b);
        ulong select(ulong i, bool b);
        TSelf write(BinaryWriter w);
        TSelf write(string filename);
        TSelf read(BinaryReader r);
        TSelf read(string filename);
        // override object.Equals
        bool Equals(object obj);
        // override object.GetHashCode
        int GetHashCode();
    }
}
