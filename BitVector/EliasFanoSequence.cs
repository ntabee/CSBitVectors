using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitVectors
{
    /**
     Elias-Fano-encoded sequence of non-decreasing natural numbers a la
     Sebastiano Vigna, "Quasi Succinct Indices", June 19, 2012, sections 3, 4 and 9.
          http://arxiv.org/pdf/1206.4300 .
    
     Given a non-decreasing sequence x_0, x_1, ..., x_(n-1) of n natural numbers,
     this class encodes the sequence in a space-efficient way.
    
     Construction:
     A. Input:
          A non-decreasing sequence 
              x_0, x_1, ..., x_(n-1) 
          of natural numbers (i.e. x_i <= x_(i+1) for all i = 0, ..., n-2)
    
     Step 1.
          let U = x_(n-1) be the upper bound of the sequence (indeed, any number >= x_(n-1) works)
          let L = max(0, log2(U/n))
          For each element x_i of the input, we will store
              + the lower L bits of x_i and
              + the remaining higher bits
          separately.
          let LOWS be a bit-vector for storing the lower bits of elements, and
              HIGHS for the higher
    
     Step 2.
          for each i = 0, ..., n-1, 
              let low_i = the lower L bits of x_i, and
              append low_i to the vector LOWS
          The length of the LOWS vector is, trivially, n*L
    
     Step 3.
          for each i = 0, ..., n-1, 
              let hi_i = the higher bits of x_i, i.e. hi_i = x_i >> L, and
              set the (hi_i + i)-th element of the HIGH vector HIGH[hi_i+i] to 1
              (also ensure all other bits of HIGH is 0)
          Under this encoding, the position of the i-th occurence of 1 in HIGH is (hi_i + i),
          thus, hi_i is obtained by HIGH.select(1, i) - i 
          (the non-decreasing property of the input sequence assures the i-th position indeed encodes the i-th value.)
          Further, the length of the HIGH vector is bounded by 3*n, as 
              + i does not exceed n (this is trivial), and
              + hi_i <= U/2^L does not exceed 2*n, because by the definition of L:
                  log2(U/n) <= L+1  <=>
                  U/n <= 2^(L+1) <=>
                  U <= 2^(L+1) * n <=>
                  U/2^L <= 2n
          
    @author ntabee (@n_tabee)
    @license MIT license
    **/

    public class EliasFanoSequence
    {
        // Initialized in the constructors
        private int NUM_LOW_BITS;
        private ulong LOW_BITS_MASK;

        private ulong upperBound_;

        private ulong maxLength_;
        private ulong pos_ = 0;
        private ulong lastVal_ = 0;

        private Bits lowBits_;
        private BitVector hiBits_;

        public EliasFanoSequence(ulong length, ulong upperBound)
        {
            maxLength_ = length;
            upperBound_ = upperBound;

            NUM_LOW_BITS = Math.Max(0, (int)Math.Floor(Math.Log(upperBound / length, 2)));
            LOW_BITS_MASK = (1UL << NUM_LOW_BITS) - 1;

            ulong numLowBits = length * (uint)NUM_LOW_BITS;
            lowBits_ = new Bits(numLowBits);

            hiBits_ = new BitVector(length);    // Give the theoretical lower bound as the initial capacity

        }

        public EliasFanoSequence(BinaryReader r)
        {
            read(r);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EliasFanoSequence push(ulong val)
        {
            if (pos_ >= maxLength_)
            {
                throw new InvalidOperationException(string.Format("EliasFanoSequence.push(): The number of pushed elements exceed the limit: {0}", maxLength_));
            }
            if (lastVal_ > val)
            {
                throw new InvalidOperationException(string.Format("EliasFanoSequence.push(): Non-decreasing restriction is violated: prev. value = {0}, given value = {1}", lastVal_, val));
            }
            if (val > upperBound_)
            {
                throw new InvalidOperationException(string.Format("EliasFanoSequence.push(): The input value {0} exceeds the upper bound {1}", val, upperBound_));
            }
            ulong high = val >> NUM_LOW_BITS;
            ulong low = val & LOW_BITS_MASK;

            lowBits_.push(low, NUM_LOW_BITS);
            hiBits_.set(pos_ + high, true);

            lastVal_ = val;
            pos_++;

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EliasFanoSequence build()
        {
            hiBits_.build();
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size()
        {
            return pos_;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong maxLength()
        {
            return maxLength_;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong upperBound()
        {
            return upperBound_;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong get(ulong i)
        {
            ulong high = hiBits_.select(i, true) - i;
		    if ( NUM_LOW_BITS == 0 ) return high;
		
            ulong lowPos = i * (ulong)NUM_LOW_BITS;
            ulong low = lowBits_.fetch64(lowPos, NUM_LOW_BITS);
            return (high << NUM_LOW_BITS) | low;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong get(int i)
        {
            return get((ulong)i);
        }

        public EliasFanoSequence write(BinaryWriter w)
        {
            w.Write(NUM_LOW_BITS);
            w.Write(LOW_BITS_MASK);

//            w.Write(INDEX_QUANTUM);
            w.Write(upperBound_);

            w.Write(maxLength_);
            w.Write(pos_);
            w.Write(lastVal_);

            lowBits_.write(w);
            hiBits_.write(w);
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EliasFanoSequence write(string filename)
        {
            BinaryWriter w = new BinaryWriter(new FileStream(filename, FileMode.Create, FileAccess.Write));
            return write(w);
        }
        public EliasFanoSequence read(BinaryReader r)
        {
            NUM_LOW_BITS = r.ReadInt32();
            LOW_BITS_MASK = r.ReadUInt64();

            upperBound_ = r.ReadUInt64();

            maxLength_ = r.ReadUInt64();
            pos_ = r.ReadUInt64();
            lastVal_ = r.ReadUInt64();

            ulong numLowBits = pos_ * (uint)NUM_LOW_BITS;
            lowBits_ = new Bits(numLowBits);

            hiBits_ = new BitVector(pos_);    // Give the theoretical lower bound as the initial capacity

            lowBits_.read(r);
            hiBits_.read(r);

            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EliasFanoSequence read(string filename)
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

            EliasFanoSequence seq = (EliasFanoSequence)obj;
            return (pos_ == seq.pos_ && lowBits_.Equals(seq.lowBits_) && hiBits_.Equals(seq.hiBits_));
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return lowBits_.GetHashCode() * hiBits_.GetHashCode();
        }

    }
}
