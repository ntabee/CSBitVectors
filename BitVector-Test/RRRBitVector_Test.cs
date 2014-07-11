using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitVectors;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace BitVector_Test
{
    [TestClass]
    public class RRRBitVector_Test
    {
        // reference impl.
        static ulong biCoefficient(ulong n, ulong k)
        {
            if (n == 0 || k == 0)
                return 0;

            if (k > n - k)
            {
                k = n - k;
            }

            BigInteger c = 1;
            for (uint i = 0; i < k; i++)
            {
                c = c * (n - i);
                c = c / (i + 1);
            }
            return (ulong)c;
        }

        [TestMethod]
        public void staticInitializer()
        {
            PrivateType pt = new PrivateType(typeof(RRRBitVector));
            ulong[][] C = (ulong[][])pt.GetStaticField("C");
            for (int n = 0; n < C.Length; n++)
            {
                for (int m = 0; m < C.Length; m++)
                {
                    Assert.AreEqual(C[n][m], biCoefficient((ulong)n, (ulong)m));
                }
            }
        }

        [TestMethod]
        public void offset()
        {
            PrivateType pt = new PrivateType(typeof(RRRBitVector));
            ulong v = 1UL;
            uint c = BitVector.popcount(v);
            ulong r = (ulong)pt.InvokeStatic("offsetOf", v, c);
            ulong v2 = (ulong)pt.InvokeStatic("ofOffset", r, c);
            Assert.AreEqual(v, v2);

            v = 0UL;
            c = BitVector.popcount(v);
            r = (ulong)pt.InvokeStatic("offsetOf", v, c);
            v2 = (ulong)pt.InvokeStatic("ofOffset", r, c);
            Assert.AreEqual(v, v2);

            Random rand = new Random();
            for (int i = 0; i < 100000; i++)
            {
                // Random-choose a bit vector of length 63
                v = (ulong)rand.Next() << 32;
                v |= (ulong)(uint)rand.Next();
                // Assuming RRBitVector.BLOCK_SIZE = 63, take the least significant 63 bits
                v &= (1UL << 63) - 1;

                c = BitVector.popcount(v);
                r = (ulong)pt.InvokeStatic("offsetOf", v, c);
                v2 = (ulong)pt.InvokeStatic("ofOffset", r, c);
                Assert.AreEqual(v, v2);
            }
        }

        [TestMethod]
        public void get()
        {
            Bits bits = new Bits(65);
            bits.push1s(1);
            bits.push0s(63);
            bits.push1s(1);

            RRRBitVector rrr = new RRRBitVector(bits);

            Assert.AreEqual(bits.size(), rrr.size());
            for (ulong i = 0; i < 65; i++)
            {
                Assert.IsTrue(bits.get(i) == rrr.get(i), string.Format("Faild at {0}.", i));
            }

            const ulong LEN = 10000;
            bits = new Bits(LEN);
            Random rand = new Random();
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                bits.set(i, b);
            }

            rrr = new RRRBitVector(bits);

            Assert.AreEqual(bits.size(), rrr.size());
            for (ulong i = 0; i < LEN; i++)
            {
                Assert.IsTrue(bits.get(i) == rrr.get(i), string.Format("Faild at {0}.", i));
            }
        }

        [TestMethod]
        public void rank()
        {
            Random rand = new Random();
            ulong LEN = 10000 + (ulong)rand.Next() % 10000;
            Bits bits = new Bits(LEN);
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                bits.set(i, b);
            }

            RRRBitVector rrr = new RRRBitVector(bits);
            BitVector bv = new BitVector(bits);
            Assert.AreEqual(bv.size(), rrr.size());
            for (ulong i = 0; i < LEN; i++)
            {
                Assert.IsTrue(bv.rank(i, true) == rrr.rank(i, true), string.Format("Faild at {0}.", i));
                Assert.IsTrue(bv.rank(i, false) == rrr.rank(i, false), string.Format("Faild at {0}.", i));
            }
        }

        [TestMethod]
        public void select()
        {
            List<ulong> values = new List<ulong>();
            values.Add(0);
            values.Add(511);
            values.Add(512);
            values.Add(1000);
            values.Add(2000);
            values.Add(2015);
            values.Add(2016);
            values.Add(2017);
            values.Add(3000);

            Bits bits1 = new Bits(values.Max());
            Bits bits0 = new Bits(values.Max());
            for (ulong i = 0; i <= values[values.Count - 1]; i++)
            {
                bits0.set(i, true);
            }

            foreach (ulong i in values)
            {
                bits1.set(i, true);
                bits0.set(i, false);
            }

            RRRBitVector rrr1 = new RRRBitVector(bits1);
            RRRBitVector rrr0 = new RRRBitVector(bits0);
            ulong counter = 0;
            foreach (ulong v in values)
            {

                Assert.AreEqual(v, rrr1.select(counter, true));
                Assert.AreEqual(v, rrr0.select(counter, false));
                counter++;
            }


            values = new List<ulong>();
            Random rand = new Random();
            ulong LEN = 10000 + (ulong)rand.Next() % 10000;
            Bits bits2 = new Bits(LEN);
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                if (b)
                {
                    values.Add(i);
                }
                bits2.set(i, b);
            }
            RRRBitVector rrr2 = new RRRBitVector(bits2);
            counter = 0;
            foreach (ulong v in values)
            {

                Assert.AreEqual(v, rrr2.select(counter, true));
                counter++;
            }

            Bits bits = new Bits(LEN);
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                bits.set(i, b);
            }

            RRRBitVector rrr = new RRRBitVector(bits);
            BitVector bv = new BitVector(bits);
            Assert.AreEqual(bv.size(true), rrr.size(true));
            Assert.AreEqual(bv.size(false), rrr.size(false));

            for (ulong i = 0; i < rrr.size(true); i++)
            {
                ulong v1 = bv.select(i, true);
                ulong v2 = rrr.select(i, true);
                Assert.IsTrue(v1 == v2, string.Format("Faild at {0}: {1} <=> {2}.", i, v1, v2));
            }
            for (ulong i = 0; i < rrr.size(false); i++)
            {
                Assert.IsTrue(bv.select(i, false) == rrr.select(i, false), string.Format("Faild at {0}.", i));
            }
        }

        [TestMethod]
        public void readAndWrite()
        {
            MemoryStream ms1 = new MemoryStream();
            BinaryWriter w1 = new BinaryWriter(ms1);

            const ulong ITER = 100000;
            Bits bits = new Bits(ITER);
            Random rand = new Random();
            for (ulong i = 0; i < ITER; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                bits.set(i, b);
            }

            RRRBitVector rrr = new RRRBitVector(bits);


            rrr.write(w1);

            Console.WriteLine(string.Format("The length of randomely generated bit-vector of length {0} = {1} bytes", rrr.size(), ms1.Length));

            ms1.Seek(0, SeekOrigin.Begin);
            RRRBitVector rrr2 = new RRRBitVector(new BinaryReader(ms1));

            Assert.AreEqual(rrr.size(), rrr2.size());
            for (ulong i = 0; i < rrr.size(); i++)
            {
                Assert.AreEqual(rrr.get(i), bits.get(i));
                Assert.AreEqual(rrr.get(i), rrr2.get(i));
            }

            Assert.AreEqual(rrr, rrr2);

        }
    }
}
