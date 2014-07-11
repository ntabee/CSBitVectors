using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitVectors;
using System.IO;

namespace BitVector_Test
{
    [TestClass]
    public class Bits_Test
    {
        [TestMethod]
        public void pushAndFetch()
        {
            Bits bits = new Bits(128);

            ulong B11111 = Convert.ToUInt64("11111", 2);

            for (int i = 0; i < 15; i++)
            {
                bits.push(B11111, 5);
            }

            Assert.AreEqual(bits.size(), 75UL);

            Assert.AreEqual(bits.dump()[0], ulong.MaxValue);
            Assert.AreEqual(bits.dump()[1], ((1UL << 11)-1) << 53);

            for (ulong i = 0; i < 15; i++)
            {
                ulong v = bits.fetch64(i * 5, 5);
                Assert.AreEqual(v, B11111);
            }
        }

        [TestMethod]
        public void setAndGet()
        {
            Bits bits = new Bits(128);

            bits.set(64UL, true);
            Assert.IsTrue(bits.get(64UL));

            ulong B11111 = Convert.ToUInt64("11111", 2);

            for (ulong i = 0; i < 128; i++)
            {
                bits.set(i, true);
            }
            try
            {
                bits.set(128UL, true);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException)
            {

            }

            for (ulong i = 0; i < 128; i++)
            {
                Assert.AreEqual(bits.get(i), true);
            }
            try
            {
                bits.get(128UL);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException)
            {

            }

        }

        [TestMethod]
        public void push1s()
        {
            Bits bits = new Bits(1024);

            for (int i = 0; i < 15; i++)
            {
                bits.push(true, 100);
            }

            Assert.AreEqual(bits.size(), 1500UL);

            Assert.AreEqual(bits.dump()[0], ulong.MaxValue);

            for (ulong i = 0; i < 1500; i++)
            {
                ulong v = bits.fetch64(i, 1);
                Assert.AreEqual(v, 1UL);
            }
        }

        [TestMethod]
        public void push0s()
        {
            Bits bits = new Bits(1024);

            for (int i = 0; i < 15; i++)
            {
                bits.push(false, 100);
            }

            Assert.AreEqual(bits.size(), 1500UL);

            Assert.AreEqual(bits.dump()[0], 0UL);

            for (ulong i = 0; i < 1500; i++)
            {
                ulong v = bits.fetch64(i, 1);
                Assert.AreEqual(v, 0UL);
            }
        }

        [TestMethod]
        public void readAndWrite()
        {
            MemoryStream ms1 = new MemoryStream();
            BinaryWriter w1 = new BinaryWriter(ms1);
            Bits bits = new Bits(128);

            ulong B11111 = Convert.ToUInt64("11111", 2);

            for (int i = 0; i < 15; i++)
            {
                bits.push(B11111, 5);
            }
            bits.write(w1);

            ms1.Seek(0, SeekOrigin.Begin);
            Bits bitsClone = new Bits(bits.size());
            bitsClone.read(new BinaryReader(ms1));

            Assert.AreEqual(bits, bitsClone);
        }
    }
}
