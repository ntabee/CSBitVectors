using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;
using System.IO;

namespace BitVectors
{
    /*
     * An implementation of Raman-Raman-Rao-style compressed bit-vector (RRR)
     *      Rajeev Raman, V. Raman and S. Srinivasa Rao, 
     *      "Succinct Indexable Dictionaries with Applications to Encoding k-ary Trees and Multisets", SODA 2002
     *      http://arxiv.org/pdf/0705.0552.pdf
     * Implementation scheme follows
     *      Francisco Claude, Gonzalo Navarro,
     *      "Practical Rank/Select Queries over Arbitrary Sequences", SPIRE 2008
     *      http://www.dcc.uchile.cl/~gnavarro/ps/spire08.1.pdf
     * and
     *      Gonzalo Navarro, Eliana Providel,
     *      "Fast, Small, Simple Rank/Select on Bitmaps", SEA 2012
     *      http://www.dcc.uchile.cl/~gnavarro/ps/sea12.1.pdf
     *      
     * Several in-detail techniques are borrowed from "sdsl-lite" library by Simon Gog et al.
     * https://github.com/simongog/sdsl-lite
     * 
     * @author ntabee (@n_tabee)
     * @license MIT license
     * 
     */
    public class RRRBitVector : IBitVector<RRRBitVector>
    {
        // Construction:
        // A. Input: 
        //      B in {0, 1}^n a bit-vector of length n, where
        //      B[0], B[1], ..., B[n-1] denote each element.
        //      this.size_ holds the length n
        private ulong size_;    // The length of the vector
        private ulong size1_;   // Total number of set bits

        // B. Pre-defined Constants:
        //      BLOCK_SIZE: a natural number, typically 2^k - 1 for some small k.
        //      In this specific implementation, we fix BLOCK_SIZE = 63 and
        //      name it "t" for notational bravity:
        //          t = BLOCK_SIZE = 63
        //      A higher value achieves better space efficiency at the expense of runtime overhead
        private const int BLOCK_SIZE = 63;
        // *IMPORTANT*
        //  BLOCK_SIZE must NOT exceed 63, since the implementation of bit-blocks heavily relies on 64bit ulong

        //
        //
        //      SUPERBLOCK_SIZE: a multiple of BLOCK_SIZE where the multiplier is
        //      a small constant of O(log n).
        //      In this specific implementation, we fix SUPERBLOCK_SIZE = BLOCK_SIZE * 32 = 2016 and
        //      name it "s" for bravity:
        //          s = SUPERBLOCK_SIZE = 2016
        private const uint SUPERBLOCK_FACTOR = 32;
        private const uint SUPERBLOCK_SIZE = BLOCK_SIZE * SUPERBLOCK_FACTOR;

        // Step 1. 
        //      The input bit-vector B is split into blocks of length t:
        //          block[0] = B[0, ..., t-1], block[1] = B[t, ..., 2t-1], ..., block[ceil(n/t)] = B[n-t-1, ..., n-1]
        //      i.e. each block is a bit-vector of length t.
        // Step 2. 
        //      For each block block[0], ..., block[ceil(n/t)], 
        //      its "class" and "offset", as defined below, is determined and stored:
        //          class[i] = class-of(block[i])
        //          offset[i] = offset-of(block[i])
        //      Definitions:
        //          Given a bit-vector v in {0, 1}^u of length u, 
        //          a. the "class" of v, class-of(v) is simply the number of 1s in B: class-of(v) = popcount(v)
        //          b. the "offset" of v, defined as follows:
        //              let c = class-of(v),
        //              let BV(u, c) = { v' | v' in {0, 1}^u, class-of(v') = c } be the
        //              set of all bit-vectors with length u and class c, and
        //              let `<=` be the lexicographic order over BV(u, c) (regarded as strings over {0, 1}),
        //              then the offset of v, offset-of(v) is the number of elements
        //              in BV(u, c) that are "smaller" than v w.r.t. this ordering:
        //              offset-of(v) = |{ v' | v' in BV(u, c) and v' < v }|
        //      N.B.
        //          Given t is fixed, the "class" and "offset" values of a t-length bit-vector v in {0, 1]^t are
        //          upper-bounded by t and C(t, class-of(v)) respectively, where C(m, n) is the usual
        //          binomial coefficient.
        //          Further, when t is fixed, there is 1-to-1 correspondence between each v in {0, 1}^t and
        //          the pair (class-of(v), offset-of(v)) which is effectively computable in both directions.
        //          Thus, the pair can be regarded as a space-reduced representation of v
        //          (as long as the space for the pair, log(t+1)+log(C(t, class-of(v)), does not exceed t.)
        //
        //          Also note that even when t is fixed, the space-requirement of the offset values
        //          log( C(t, class-of(v)) ) varies with class-of(v).
        //          So the offset store is indeed a sequence of variable-length values.
        //          This means random-access to an arbitrary element offset[i] in the offset store is 
        //          not (trivially) possible, which will be addressed in Step 3.
        private Bits classValues_;
        private Bits offsetValues_;
        private const int BITS_PER_CLASS = 6;  // log2(BLOCK_SIZE+1)
        // will be calculated in the static initializer
        private static uint MAX_BITS_PER_OFFSET = 0;
        
        // Step 3. 
        //      The input B is again split into chunks, called "super-blocks", of length 
        //          s = SUPERBLOCK_SIZE = BLOCK_SIZE * SUPERBLOCK_FACTOR:
        //          sb[0] = B[0, ,,,. s-1], sb[1] = B[s, ..., 2s-1], ..., sb[ceil(n/s)] = B[n-s-1, ..., n-1]
        //      where SUPERBLOCK_FACTOR is a small consant of O(n) and fixed to 32 in our specific impelementation.
        //      For each i = 0, ..., ceil(n/s),
        //          a pair(rankSum[i], offset[i]) is stored, where
        //              rankSum[i] = sum_{ j = 0, ..., i-1 } popcount(sb[j])
        //              is the number of 1s in B before the i-th super-block, and
        //              offset[i] is the bit-position of the (i * SUPERBLOCK_FACTOR)-th block's offset value, 
        //              i.e. the bit sequence offsetValues_[offset[i], offset[i]+len-1] gives the offset value of
        //              the (i * SUPERBLOCK_FACTOR)-th block, where len is determined from the class value of the block.
        //              
        private EliasFanoSequence rankSamples_;
        private EliasFanoSequence offsetPosSamples_;

        // C[n][m] holds C(n, m).  We define C(0, m) = C(n, 0) = 0 for all m, n
        private static ulong[][] C = new ulong[BLOCK_SIZE + 1][];

        // holds log2(C(t, n)) n = 0, ..., t 
        private static uint[] BITS_OF_OFFSETS_OF_CLASS = new uint[BLOCK_SIZE+1];

        // cf. http://stackoverflow.com/questions/12983731/algorithm-for-calculating-binomial-coefficient
        public static ulong BinomialCoefficient(int n, int k)
        {
            if (k == 0) { return 0; }
            if (n == 0) { return 0; }
            if (k > n) { return 0; }
            if (n == k) { return 1; } // only one way to chose when n == k
            if (k > n - k) { k = n - k; } // Everything is symmetric around n-k, so it is quicker to iterate over a smaller k than a larger one.
//            ulong c = 1;
            BigInteger c = 1;
            for (int i = 1; i <= k; i++)
            {
                c *= (ulong)n;
                n--;
                c /= (ulong)i;
            }
            if (c > ulong.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("C({0}, {1}) = {2} is too large to be an ulong.", n, k, c.ToString())
                );
            }
            return (ulong)c;
        }
        static RRRBitVector()
        {
            for (int n = 0; n <= BLOCK_SIZE; n++)
            {
                C[n] = new ulong[BLOCK_SIZE+1];
                for (int m = 0; m <= n; m++)
                {
                    C[n][m] = BinomialCoefficient(n, m);
                }
                ulong elementsInTheClass = BinomialCoefficient(BLOCK_SIZE, n);
                uint bits = (uint)Math.Ceiling(Math.Log(elementsInTheClass+1, 2));
                BITS_OF_OFFSETS_OF_CLASS[n] = bits;
                MAX_BITS_PER_OFFSET = Math.Max(MAX_BITS_PER_OFFSET, bits);
            }
        }

        // Recall the definition: offset-of(v) = |{ v' | v' in BV(u, c) and v' < v }|
        //      where:
        //          c = class-of(v) = popcount(v),
        //          BV(u, c) = { v' | v' in {0, 1}^u, class-of(v') = c } is the set of all bit-vectors of length u and class c, and
        //          `<=` (thus, `<`) is the lexicographic order over BV(u, c) regarded as strings over {0, 1}
        // Now, suppose the leftmost bit of v, v[0] = 0.  Then offset-of(v) is excactly the same as
        // offset-of(v[1, ..., u-1]) (where [i, ..., j] denotes a slice of a sequence)
        //      if (v[0] = 0)
        //          offset-of(v) = offset-of(v[1, ..., u-1])
        // Otherwise, i.e. if v[0] = 1, then, by the definition of lexicographic ordering, offset-of(v) is
        // greater than those bit-vectors begin with 0: 
        //      v[0] = 1 => v > v' (for all v' in BV(u, c), v'[0] = 0) 
        // The number of such v's is given by C(u-1, c), so:
        //      if (v[0] = 1)
        //          offset-of(v) = C(u-1, c) + alpha
        // It would not be difficult to see the remainder "alpha" is given by the offset of v[1, ..., u-1] in BV(u-1, c-1):
        //      if (v[0] = 1)
        //          offset-of(v) = C(u-1, c) + offset-of(v[1, ..., u-1])
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong offsetOf(ulong v, uint clazz)
        {
            if (clazz == 0)
            {
                return 0UL;
            }
            ulong offset = 0;
            for (int i = BLOCK_SIZE - 1; i >= 0; i--)
            {
                if ((v & (1UL << i)) != 0)
                {
                    offset += C[i][clazz];
                    clazz--;
                }
            }
            return offset;
        }
        // Inverse of offsetOf()
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ofOffset(ulong offset, uint clazz)
        {
            if (clazz == 0)
            {
                return 0UL;
            }
            ulong v = 0UL;
            ulong c;
            int i = BLOCK_SIZE - 1;
            do
            {
                c = C[i][clazz];
                if (offset >= c)
                {
                    v |= (1UL << i);
                    offset -= c;
                    clazz--;
                    if (clazz <= 0)
                    {
                        return v;
                    }
                }
                i--;
            } while (i >= 0);
            return v;
        }

        private void build(Bits bits)
        {
            ulong size = bits.size();
            ulong nBlocks = (size + BLOCK_SIZE - 1) / BLOCK_SIZE;
            ulong nSuperBlocks = (size + SUPERBLOCK_SIZE -1) / SUPERBLOCK_SIZE;

            classValues_ = new Bits(BITS_PER_CLASS * nBlocks);
            offsetValues_ = new Bits((ulong)MAX_BITS_PER_OFFSET * nBlocks);
            rankSamples_ = new EliasFanoSequence(nSuperBlocks, size);
            offsetPosSamples_ = new EliasFanoSequence(nSuperBlocks, size);

            ulong rankSum = 0;

            // Step 1.  Split the input bit-vector into blocks of length BLOCK_SIZE:
            for (ulong i = 0; i < nBlocks; i++)
            {
                // Step 3.
                //      Construct a super-block summary for every SUPERBLOCK_FACTOR (= 32) blocks.
                //      This step is performed before Step 2. as the summaries to the _previous_ block is stored.
                //      
                if ((i % SUPERBLOCK_FACTOR) == 0)
                {
                    ulong sampleIndex = i / SUPERBLOCK_FACTOR;
                    offsetPosSamples_.push(offsetValues_.size());
                    rankSamples_.push(rankSum);
                }

                // Step 2.  Encode each block v as a pair (class-of(v), offset-of(v))
                ulong pos = i * BLOCK_SIZE;
                ulong block = bits.fetch64(pos, BLOCK_SIZE);

                uint clazz = BitVector.popcount(block);

                classValues_.push(clazz, BITS_PER_CLASS);
                
                ulong offset = offsetOf(block, clazz);
                offsetValues_.push(offset, (int)BITS_OF_OFFSETS_OF_CLASS[clazz]);

                rankSum += clazz;
            }

            size_ = size;
            size1_ = rankSum;

            rankSamples_.build();
            offsetPosSamples_.build();

#if DEBUG
            // Checks the correctnes of encoding
            for (ulong i = 0; i < nBlocks; i++)
            {
                ulong pos = i * BLOCK_SIZE;
                ulong block = bits.fetch64(pos, BLOCK_SIZE);

                uint clazz = BitVector.popcount(block);

                ulong offset = offsetOf(block, clazz);

                uint clazz2 = classOfBlock(i);
                ulong offset2 = offsetOfBlock(i, clazz);
                ulong block2 = fetchBlock(i, clazz);
                Debug.Assert(clazz == clazz2, string.Format("class: {0} != {1} ({2})", clazz, clazz2, i));
                Debug.Assert(offset == offset2, string.Format("offset: {0} != {1} ({2})", offset, offset2, i));
                Debug.Assert(block == block2, string.Format(
                    "block: {0} != {1} ({2})",
                    Convert.ToString((long)block, 2),
                    Convert.ToString((long)block2, 2), i));
            }
#endif
        }

        public RRRBitVector(Bits bits) 
        {
            build(bits);
        }

        public RRRBitVector(BinaryReader r)
        {
            read(r);
        }

        public RRRBitVector(string filename)
        {
            read(filename);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size()
        {
            return size_;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong size(bool b)
        {
            return b ? size1_ : (size_ - size1_);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint classOfBlock(ulong i)
        {
            return classValues_.fetch32(i * BITS_PER_CLASS, BITS_PER_CLASS);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong offsetOfBlock(ulong i, uint clazz)
        {
            int offsetLen = (int)BITS_OF_OFFSETS_OF_CLASS[clazz];

            ulong superBlockIndex = i / SUPERBLOCK_FACTOR;
            ulong offsetPos = offsetPosSamples_.get(superBlockIndex);
            ulong j = superBlockIndex * SUPERBLOCK_FACTOR;
            while (j < i)
            {
                uint jthClass = classOfBlock(j);
                offsetPos += BITS_OF_OFFSETS_OF_CLASS[jthClass];
                j++;
            }
            return offsetValues_.fetch64(offsetPos, offsetLen);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong fetchBlock(ulong i, uint clazz)
        {
            return ofOffset(offsetOfBlock(i, clazz), clazz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong fetchBlock(ulong i)
        {
            uint clazz = classOfBlock(i);
            return ofOffset(offsetOfBlock(i, clazz), clazz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool get(ulong i)
        {
            ulong blockIndex = i / BLOCK_SIZE;
            uint clazz = classOfBlock(blockIndex);
            if (clazz == 0)
            {
                // all bit of the block is 0
                return false;
            }
            if (clazz == BLOCK_SIZE)
            {
                // all bit of the block is 1
                return true;
            }
            ulong block = fetchBlock(blockIndex, clazz);
            int offsetInBlock = (int)(i % BLOCK_SIZE);
            ulong mask = 1UL << (BLOCK_SIZE - 1 - offsetInBlock);
            return (block & mask) != 0;
        }

        private ulong rank1(ulong i)
        {
            ulong blockIndex = i / BLOCK_SIZE;
            ulong superBlockIndex = blockIndex / SUPERBLOCK_FACTOR;
            ulong rank = rankSamples_.get(superBlockIndex);
            if (superBlockIndex + 1 < rankSamples_.size())
            {
                // fetch the next sample
                ulong rankNext = rankSamples_.get(superBlockIndex + 1);
                ulong delta = rankNext - rank;
                if (delta == 0)
                {
                    // all the bit in the superBlockIndex-th super-block is 0
                    return rank;
                }
                else if (delta == SUPERBLOCK_SIZE)
                {
                    // all the bit in the super-block is 1
                    ulong superBlockHead = superBlockIndex * SUPERBLOCK_SIZE;
                    return rank + (i - superBlockHead);
                }
            }
            ulong j = superBlockIndex * SUPERBLOCK_FACTOR;
            while (j < blockIndex)
            {
                rank += classOfBlock(j);
                j++;
            }
            ulong block = fetchBlock(blockIndex);
            int posInTheBlock = (int)(i % BLOCK_SIZE);
            ulong mask = (1UL << (BLOCK_SIZE - posInTheBlock)) - 1; // least significant (BLOCK_SIZE - posInTheBlock) bits are set
            mask = ~mask;   // most significant posInTheBlock bits are set
            return rank + BitVector.popcount(block & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong rank(ulong i, bool b)
        {
            if (!b)
            {
                return i - rank1(i);
            }
            return rank1(i);
        }

        private ulong select1(ulong i)
        {

            if (i >= size(true))
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("RRRBitVector.select1(): the argument {0} exceeds the total number of 1s = {1}", i, size(true))
                    );
            }
            ulong left = 0;
            ulong right = rankSamples_.size() - 1;
            while (left < right)
            {
                ulong pivot = (left + right) >>1; // / 2;
                ulong rankAtThePivot = rankSamples_.get(pivot);
                if (i < rankAtThePivot) 
                { 
                    right = pivot; 
                }
                else 
                { 
                    left = pivot + 1; 
                }
            }
            right--;

            ulong j = right * SUPERBLOCK_FACTOR;
            ulong rank = rankSamples_.get(right);
            ulong delta = rankSamples_.get(right+1) - rank;
            if (delta == SUPERBLOCK_SIZE)
            {
                // every bit in the left-th super-block is 1
                return j * BLOCK_SIZE + (i - rank);
            }

            ulong nBlocks = (size() + BLOCK_SIZE - 1) / BLOCK_SIZE;
            ulong origI = i;
            i -=  rank;
            while (true)
            {
                Debug.Assert(j < nBlocks, string.Format("j exceeded {0}, i = {1}, popcount = {2}", nBlocks, origI, rank1(size()-1)));
                uint c = classOfBlock(j);

                if (i < c) { break; }
                j++;
                i -= c;
            }
            ulong block = fetchBlock(j);
            ulong _64bitAligned = block << (64 - BLOCK_SIZE);
            return j * BLOCK_SIZE + BitVector.select64(_64bitAligned, (int)i, true);
        }

        private ulong select0(ulong i)
        {
            if (i >= size(false))
            {
                throw new ArgumentOutOfRangeException(
                    string.Format("RRRBitVector.select0(): the argument {0} exceeds the total number of 0s = {1}", i, size(false))
                    );
            }
            ulong left = 0;
            ulong right = rankSamples_.size() - 1;
            while (left < right)
            {
                ulong pivot = (left + right) >>1; // / 2;
                ulong rankAtThePivot = pivot * SUPERBLOCK_SIZE - rankSamples_.get(pivot);
                if (i < rankAtThePivot)
                {
                    right = pivot;
                }
                else
                {
                    left = pivot + 1;
                }
            }
            right--;

            ulong j = right * SUPERBLOCK_FACTOR;
            ulong rank = rankSamples_.get(right);
            ulong delta = rankSamples_.get(right + 1) - rank;
            if (delta == 0)
            {
                // every bit in the left-th super-block is 0
                return j * BLOCK_SIZE + (i - (rank + 1));
            }

            i -= right * SUPERBLOCK_SIZE - rank;
            while (true)
            {
                uint c = classOfBlock(j);
                uint r = (uint)BLOCK_SIZE - c;
                if (i < r) { break; }
                j++;
                i -= r;
            }
            ulong block = fetchBlock(j);
            ulong _64bitAligned = block << (64 - BLOCK_SIZE);
            return j * BLOCK_SIZE + BitVector.select64(_64bitAligned, (int)i, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong select(ulong i, bool b)
        {
            return b ? select1(i) : select0(i);
        }

        public RRRBitVector write(BinaryWriter w)
        {
            w.Write(size_);
            w.Write(size1_);

            classValues_.write(w);
            offsetValues_.write(w);
            rankSamples_.write(w);
            offsetPosSamples_.write(w);
            return this;
        }
        public RRRBitVector write(string filename)
        {
            BinaryWriter w = new BinaryWriter(new FileStream(filename, FileMode.Create, FileAccess.Write));
            return write(w);
        }

        public RRRBitVector read(BinaryReader r)
        {
            size_ = r.ReadUInt64();
            size1_ = r.ReadUInt64();


            classValues_ = new Bits(r);
            offsetValues_ = new Bits(r);
            rankSamples_ = new EliasFanoSequence(r);
            offsetPosSamples_ = new EliasFanoSequence(r);
            return this;
        }
        public RRRBitVector read(string filename)
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

            RRRBitVector bv = (RRRBitVector)obj;
            return (
                size_ == bv.size_ && 
                size1_ == bv.size1_ &&
                classValues_.Equals(bv.classValues_) &&
                offsetValues_.Equals(bv.offsetValues_)
                );
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return classValues_.GetHashCode() * offsetValues_.GetHashCode();
        }
    }
}
