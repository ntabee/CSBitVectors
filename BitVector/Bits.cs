using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BitVectors
{
    /*
     * An implementation of plain-vanilla bit-vector without rank()/select() support.
     * The roles of the class are
     *      1. to provide a random-access interface to an arbitrary subsequence (of up-to 64bit length)
     *      2. to act as a source of other, more sophisticated bit-vector implementations.
     * 
     * @author ntabee (@n_tabee)
     * @license MIT license
     * */
    public class Bits
    {
        private IList<ulong> data_;
        private ulong pos_ = 0;

        public Bits(ulong initialCapacity)
        {
            data_ = Enumerable.Repeat(0UL, (int)((initialCapacity + 63) / 64)).ToList();         
        }
        public Bits(IList<ulong> bits)
        {
            data_ = bits;
        }
        public Bits(IEnumerable<ulong> bits)
        {
            data_ = bits.ToList();
        }
        public Bits(IList<byte> bits)
            : this(((ulong)bits.Count) * 8)
        {
            push(bits);
        }
        public Bits(IEnumerable<byte> bits)
            : this(bits.ToList())
        {

        }

        public Bits(BinaryReader r)
        {
            data_ = new List<ulong>();
            pos_ = (ulong)data_.Count * 64;
            read(r);
        }

        public Bits(string filename)
        {
            data_ = new List<ulong>();
            read(filename);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits set(ulong pos, bool b)
        {
            int indexInList = (int)(pos / 64);
            int offset = (int)(pos % 64);
            ulong block = data_[indexInList];
            ulong mask = (1UL << (63 - offset));
            if (b)
            {
                block |= mask;
            }
            else
            {
                block &= ~mask;
            }
            data_[indexInList] = block;

            if (pos+1 >= pos_)
            {
                pos_ = pos+1;
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits set(ulong pos)
        {
            int indexInList = (int)(pos / 64);
            int offset = (int)(pos % 64);
            ulong block = data_[indexInList];
            ulong mask = (1UL << (63 - offset));
            block |= mask;
            data_[indexInList] = block;

            if (pos + 1 >= pos_)
            {
                pos_ = pos + 1;
            }
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits unset(ulong pos)
        {
            int indexInList = (int)(pos / 64);
            int offset = (int)(pos % 64);
            ulong block = data_[indexInList];
            ulong mask = (1UL << (63 - offset));
            block &= ~mask;
            data_[indexInList] = block;

            if (pos + 1 >= pos_)
            {
                pos_ = pos + 1;
            }
            return this;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool get(ulong pos)
        {
            int indexInList = (int)(pos / 64);
            int offset = (int)(pos % 64);
            ulong block = data_[indexInList];

            return (block & (1UL << (63 - offset))) != 0;
        }

        public Bits push(ulong bits, int nbits)
        {
            if (nbits == 0)
            {
                return this;
            }
               
            if (nbits < 0 || nbits > 64)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("Bits.push(): the given number of bits {0} exceeds the valid range: [0, 64].", nbits)
                );
            }
            ulong left = pos_;
            ulong right = left + (ulong)nbits - 1;

            int leftIndexInList = (int)(left / 64);
            int offsetInList = (int)(left % 64);

            int rightIndexInList = (int)(right / 64);

            if (leftIndexInList != rightIndexInList)
            {
                // 64bit boundary is crossed over
                int nbitsForLeft = 64 - offsetInList;
                int nbitsForRight = nbits - nbitsForLeft;
                ulong bitsForLeft = bits >> nbitsForRight;
                push(bitsForLeft, nbitsForLeft);
                push(bits, nbitsForRight);
            }
            else
            {
                // no boundary crossing
                if (leftIndexInList >= data_.Count)
                {
                    data_.Add(0UL);
                }
                ulong block = data_[leftIndexInList];

                ulong mask = (nbits == 64) ? ulong.MaxValue : (1UL << nbits) - 1UL;
                bits &= mask;
                bits <<= (64 - (offsetInList + nbits));
                block |= bits;
                data_[leftIndexInList] = block;
                pos_ += (ulong)nbits;
            }
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(long bits, int nbits)
        {
            return push((ulong)bits, nbits);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(uint bits, int nbits)
        {
            return push((ulong)bits, nbits);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(int bits, int nbits)
        {
            return push((ulong)bits, nbits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(IList<byte> l) 
        {
            foreach (byte v in l) {
                push(v, sizeof(byte)*8);
            }
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push1s(int nbits)
        {
            const ulong _1s = ulong.MaxValue;
            for (int i = 0; i < nbits / 64; i++)
            {
                push(_1s, 64);
            }
            int r = nbits % 64;
            if (r > 0)
            {
                push(_1s, r);
            }
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push0s(int nbits)
        {
            const ulong _0s = 0UL;
            for (int i = 0; i < nbits / 64; i++)
            {
                push(_0s, 64);
            }
            int r = nbits % 64;
            if (r > 0)
            {
                push(_0s, r);
            }
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(bool b, int nbits)
        {
            return (b) ? push1s(nbits) : push0s(nbits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bits push(bool b)
        {
            return (b) ? push1s(1) : push0s(1);
        }

        private ulong internalFetch64(ulong pos, int nbits)
        {
            if (nbits == 0)
            {
                return 0UL;
            }
            Debug.Assert(nbits > 0);
            ulong left = pos;
            ulong right = left + (ulong)nbits - 1;

            int leftIndexInList = (int)(left >> 6); //(int)(left / 64);
            if (leftIndexInList >= data_.Count)
            {
                return 0UL;
            }
            int offsetInList = (int)(left & 63); //(int)(left % 64);

            int rightIndexInList = (int)(right >> 6); //(int)(right / 64);

            if (leftIndexInList != rightIndexInList)
            {
                // 64bit boundary is crossed over
                int nbitsForLeft = 64 - offsetInList;
                int nbitsForRight = nbits - nbitsForLeft;

                ulong leftBits = internalFetch64(pos, nbitsForLeft);
                ulong rightBits = internalFetch64(pos + (ulong)nbitsForLeft, nbitsForRight);
                return (leftBits << nbitsForRight) | rightBits;
            }
            else
            {
                // no boundary crossing
                ulong block = data_[leftIndexInList];
                if (nbits == 64)
                {
                    Debug.Assert(offsetInList == 0);
                    return block;
                }

                ulong mask = (1UL << nbits) - 1;
                int maskShift = 64 - (offsetInList + nbits);
                mask <<= maskShift;
                return (block & mask) >> maskShift;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong fetch64(ulong pos, int nbits)
        {
            if (nbits < 0 || nbits > 64)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("Bits.fetch64(): the given number of bits {0} exceeds the valid range: [0, 64].", nbits)
                );
            }
            return internalFetch64(pos, nbits);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong fetch64(ulong pos)
        {
            return internalFetch64(pos, 64);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint fetch32(ulong pos, int nbits)
        {
            if (nbits < 0 || nbits > 32)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("Bits.fetch32(): the given number of bits {0} exceeds the valid range: [0, 32].", nbits)
                );
            }
            return (uint)internalFetch64(pos, nbits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int fetchInt32(ulong pos, int nbits)
        {
            if (nbits < 0 || nbits > 32)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("Bits.fetchInt32(): the given number of bits {0} exceeds the valid range: [0, 32].", nbits)
                );
            }
            return (int)internalFetch64(pos, nbits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long fetchInt64(ulong pos, int nbits)
        {
            return (long)internalFetch64(pos, nbits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IList<ulong> dump()
        {
            return data_;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size()
        {
            return pos_;
        }
        public Bits write(BinaryWriter w)
        {
            w.Write(pos_);

            int size = (int)((pos_ + 63) / 64);
//            size = data_.Count;
            w.Write(size);
            for (int i = 0; i < size; i++)
            {
                w.Write(data_[i]);
            }
            return this;
        }
        public Bits write(string filename)
        {
            BinaryWriter w = new BinaryWriter(new FileStream(filename, FileMode.Create, FileAccess.Write));
            return write(w);
        }
        public Bits read(BinaryReader r)
        {
            pos_ = r.ReadUInt64();

            int size = r.ReadInt32();

            data_.Clear();
            if (size > data_.Count)
            {
                data_.Clear();
                data_ = new ulong[size];
            }
            for (int i = 0; i < size; i++)
            {
                data_[i] = r.ReadUInt64();
            }
            return this;
        }
        public Bits read(string filename)
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

            Bits bits = (Bits)obj;

            int validDataRange = (int)((this.pos_ + 63) / 64);
            return (bits.pos_ == this.pos_ && bits.data_.Take(validDataRange).SequenceEqual(this.data_.Take(validDataRange)));
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            ulong sum = 19UL;
            foreach (ulong v in data_) {
                sum += 31*v;
            }
            return (int)sum;
        }
    }
}
