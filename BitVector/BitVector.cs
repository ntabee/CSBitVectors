using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;

namespace BitVectors
{
    /**
     * succinct bit-vector in C#
     * ported from a C++ impl. https://code.google.com/p/shellinford/ by echizentm, and 
     * partially from https://code.google.com/p/wat-array/ by Daisuke.Okanohara.
     * 
     * Fast rank()/select() support, no data compression
     * 
     * @author ntabee (@n_tabee)
     * @license MIT license
     **/

    public class BitVector : IBitVector<BitVector>
    {
        private const int SMALL_BLOCK_SIZE = 64;
        private const int LARGE_BLOCK_SIZE = 512;
        private const int BLOCK_RATE = 8;

        // Utility methods for 64bit vectors
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint popcount(ulong B)
        {
            B = B - ((B >> 1) & 0x5555555555555555UL);
            B = (B & 0x3333333333333333UL) +
                ((B >> 2) & 0x3333333333333333UL);
            return (uint)((((B + (B >> 4)) & 0x0f0f0f0f0f0f0f0fUL) * 0x0101010101010101UL) >> 56);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint maskedPopcount(ulong x, ulong offset)
        {
            //            if (offset == 0) return 0;
            return popcount(x & ((1UL << (int)offset) - 1));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint rank64OfReversed(ulong x, int i, bool b)
        {
            if (!b) { x = ~x; }
            x <<= (SMALL_BLOCK_SIZE - i);
            return popcount(x);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint select64OfReversed(ulong x, int i, bool b)
        {
            if (!b) { x = ~x; }
            ulong x1 = ((x & 0xaaaaaaaaaaaaaaaaUL) >> 1)
                        + (x & 0x5555555555555555UL);
            ulong x2 = ((x1 & 0xccccccccccccccccUL) >> 2)
                        + (x1 & 0x3333333333333333UL);
            ulong x3 = ((x2 & 0xf0f0f0f0f0f0f0f0UL) >> 4)
                        + (x2 & 0x0f0f0f0f0f0f0f0fUL);
            ulong x4 = ((x3 & 0xff00ff00ff00ff00UL) >> 8)
                        + (x3 & 0x00ff00ff00ff00ffUL);
            ulong x5 = ((x4 & 0xffff0000ffff0000UL) >> 16)
                        + (x4 & 0x0000ffff0000ffffUL);

            ulong iUL = (ulong)i;
            iUL++;
            int pos = 0;
            ulong v5 = x5 & 0xffffffffUL;
            if (iUL > v5) { iUL -= v5; pos += 32; }
            ulong v4 = (x4 >> pos) & 0x0000ffffUL;
            if (iUL > v4) { iUL -= v4; pos += 16; }
            ulong v3 = (x3 >> pos) & 0x000000ffUL;
            if (iUL > v3) { iUL -= v3; pos += 8; }
            ulong v2 = (x2 >> pos) & 0x0000000fUL;
            if (iUL > v2) { iUL -= v2; pos += 4; }
            ulong v1 = (x1 >> pos) & 0x00000003UL;
            if (iUL > v1) { iUL -= v1; pos += 2; }
            ulong v0 = (x >> pos) & 0x00000001UL;
            if (iUL > v0) { iUL -= v0; pos += 1; }
            return (uint)pos;
        }

        // cf. http://matthewarcus.wordpress.com/2012/11/18/reversing-a-64-bit-word/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong reverseBits(ulong n)
        {
            const ulong m0 = 0x5555555555555555UL;
            const ulong m1 = 0x0300c0303030c303UL;
            const ulong m2 = 0x00c0300c03f0003fUL;
            const ulong m3 = 0x00000ffc00003fffUL;
            n = ((n>>1)&m0) | (n&m0)<<1;

            // swapbits<T> = (T m, int k, T p) => {
            //      T q = ((p>>k)^p)&m;
            //      return p^q^(q<<k);
            // }
            //n = swapbits<uint64_t, m1, 4>(n);
            ulong q = ((n >> 4) ^ n) & m1;
            n = n ^ q ^ (q << 4);
            //n = swapbits<uint64_t, m2, 8>(n);
            q = ((n >> 8) ^ n) & m2;
            n = n ^ q ^ (q << 8);
            //n = swapbits<uint64_t, m3, 20>(n);
            q = ((n >> 20) ^ n) & m3;
            n = n ^ q ^ (q << 20);

            return (n >> 34) | (n << 30);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint select64(ulong x, int i, bool b)
        {
            return select64OfReversed(reverseBits(x), i, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint rank64(ulong x, int i, bool b)
        {
            return rank64OfReversed(reverseBits(x), i, b);
        }


        // END: Utility methods

        private List<ulong> v_;
        private List<ulong> r_;
        private ulong size_;
        private ulong size1_;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size() { return size_; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size(bool b)
        {
            return b ? (size1_) : (size_ - size1_);
        }
        public BitVector()
        {
            v_ = new List<ulong>();
            r_ = new List<ulong>();
            this.size_ = 0;
            this.size1_ = 0;
        }
        public BitVector(ulong size)
        {
            v_ = Enumerable.Repeat(0UL, (int)((size + 63) / 64)).ToList();
            r_ = new List<ulong>();
            this.size_ = 0;
            this.size1_ = 0;
        }
        public BitVector(Bits bits)
        {
            v_ = new List<ulong>(bits.dump().Count);
            r_ = new List<ulong>();
            foreach (ulong v in bits.dump())
            {
                v_.Add(reverseBits(v));
            }
            size_ = bits.size();
            build();
        }
        public BitVector(IList<byte> bytes)
        {
            v_ = new List<ulong>();
            r_ = new List<ulong>();

            ulong val = 0UL;
            ulong i = 0;
            foreach (byte b in bytes)
            {
                val = (val << 8) | b;
                ++i;
                if (i % 8 == 0)
                {
                    v_.Add(reverseBits(val));
                }
            }
            if (i % 8 > 0)
            {
                v_.Add(reverseBits(val));
                val = 0UL;
            }
            size_ = i*8;
            build();
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitVector clear()
        {
            v_.Clear();
            r_.Clear();
            this.size_ = 0;
            this.size1_ = 0;
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool get(ulong i)
        {
            if (i >= size())
            {
                throw new IndexOutOfRangeException(
                    string.Format("BitVector.get(): the argument {0} exceeds the vector length {1}", i, size())
                    );
            }
            int q = (int)(i / SMALL_BLOCK_SIZE);
            int r = (int)(i % SMALL_BLOCK_SIZE);
            ulong m = 1UL << r;
            return ((v_[q] & m) != 0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitVector set(ulong i, bool b)
        {
            if (i >= size()) { size_ = i + 1; }
            int q = (int)(i / SMALL_BLOCK_SIZE);
            int r = (int)(i % SMALL_BLOCK_SIZE);
            while (q >= v_.Count) { v_.Add(0); }
            ulong m = 0x1UL << r;
            if (b) { 
                v_[q] |= m; 
            } else { 
                v_[q] &= ~m; 
            }
            return this;
        }

        // Construct the rank dictionary for subsequent queries
        public BitVector build()
        {
            r_.Clear();
            r_.Capacity = (v_.Count + BLOCK_RATE - 1) / BLOCK_RATE;
            size1_ = 0;
            for (int i = 0; i < v_.Count; i++)
            {
                if (i % BLOCK_RATE == 0)
                {
                    r_.Add(size(true));
                }
                size1_ += rank64OfReversed(v_[i], SMALL_BLOCK_SIZE, true);
            }
            return this;
        }

        public ulong rank(ulong i, bool b)
        {
            if (i > size())
            {
                throw new IndexOutOfRangeException(
                    string.Format("BitVector.rank(): the 1st argument {0} exceeds the vector length {1}", i, size())
                    );
            }
            if (i == 0) { return 0; }
            i--;
            int q_large = (int)(i / LARGE_BLOCK_SIZE);
            int q_small = (int)(i / SMALL_BLOCK_SIZE);
            int r = (int)(i % SMALL_BLOCK_SIZE);

            ulong rank = r_[q_large];
            if (!b) { rank = (ulong)q_large * LARGE_BLOCK_SIZE - rank; }
            int begin = (int)(q_large * BLOCK_RATE);
            for (int j = begin; j < q_small; j++)
            {
                rank += rank64OfReversed(v_[j], SMALL_BLOCK_SIZE, b);
            }
            rank += rank64OfReversed(v_[q_small], r + 1, b);
            return rank;
        }

        public ulong select(ulong i, bool b)
        {
            if (i >= this.size(b)) { 
                throw new ArgumentOutOfRangeException(
                    string.Format("BitVector::select(): the 1st argument {0} exceeds the vector length {1}", i, size(b))
                ); 
            }

            var r = this.r_;
            int left = 0;
            int right = r.Count;
            while (left < right)
            {
                int pivot = (left + right) >> 1; // / 2;
                ulong rank = r[pivot];
                if (!b) { rank = ((ulong)pivot * LARGE_BLOCK_SIZE) - rank; }
                if (i < rank) { right = pivot; }
                else { left = pivot + 1; }
            }
            right--;

            if (b) { i -= r[right]; }
            else { i -= (ulong)right * LARGE_BLOCK_SIZE - r[right]; }
            int j = right * BLOCK_RATE;
            while (true)
            {
                uint rank = rank64OfReversed(v_[j], SMALL_BLOCK_SIZE, b);
                if (i < rank) { break; }
                j++;
                i -= rank;
            }
            return (ulong)j * SMALL_BLOCK_SIZE + select64OfReversed(v_[j], (int)i, b);
        }

        public BitVector write(BinaryWriter w)
        {
            w.Write(size_);
            w.Write(size1_);

            int size = 0;
            size = v_.Count;
            w.Write(size);
            for (int i = 0; i < size; i++)
            {
                w.Write(v_[i]);
            }
            size = r_.Count;
            w.Write(size);
            for (int i = 0; i < size; i++)
            {
                w.Write(r_[i]);
            }
            return this;
        }
        public BitVector write(string filename)
        {
            BinaryWriter w = new BinaryWriter(new FileStream(filename, FileMode.Create, FileAccess.Write));
            return write(w);
        }
        public BitVector read(BinaryReader r)
        {
            clear();
            size_ = r.ReadUInt64();
            size1_ = r.ReadUInt64();

            int size = r.ReadInt32();
            v_.Capacity = size;
            ulong x;
            for (int i = 0; i < size; i++)
            {
                x = r.ReadUInt64();
                v_.Add(x);
            }
            size = r.ReadInt32();
            r_.Capacity = size;
            for (int i = 0; i < size; i++)
            {
                r_.Add(r.ReadUInt64());
            }
            return this;
        }
        public BitVector read(string filename)
        {
            BinaryReader r = new BinaryReader(new FileStream(filename, FileMode.Open, FileAccess.Read));
            return read(r);
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            BitVector bv = (BitVector)obj;
            return (size_ == bv.size_ && Enumerable.SequenceEqual(v_, bv.v_));
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            ulong sum = 19UL;
            foreach (ulong v in v_)
            {
                sum += 31 * v;
            }
            return (int)sum;
        }
    }
}
  



