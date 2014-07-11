using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BitVectors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitVector_Test
{
    [TestClass]
    public class BitVector_Test
    {
        List<ulong> values;
        BitVector bv1;
        BitVector bv0;

        [TestInitialize]
        public void Setup()
        {
            values = new List<ulong>();
            values.Add(0);
            values.Add(511);
            values.Add(512);
            values.Add(1000);
            values.Add(2000);
            values.Add(3000);

            bv1 = new BitVector();
            bv0 = new BitVector();
            for (ulong i = 0; i <= values[values.Count - 1]; i++)
            {
                bv0.set(i, true);
            }

            foreach (ulong i in values)
            {
                bv1.set(i, true);
                bv0.set(i, false);
            }

            bv1.build();
            bv0.build();
        }

        [TestMethod]
        public void size()
        {
            Assert.AreEqual(values[values.Count - 1] + 1, bv1.size());
            Assert.AreEqual((ulong)values.Count, bv1.size(true));
            Assert.AreEqual(values[values.Count - 1] + 1, bv0.size());
            Assert.AreEqual((ulong)values.Count, bv0.size(false));
        }

        [TestMethod]
        public void get()
        {
            foreach (ulong v in values)
            {
                Assert.AreEqual(true, bv1.get(v));
                Assert.AreEqual(false, bv0.get(v));
            }
        }

        [TestMethod]
        public void rankBeforeBuild()
        {
            BitVector bv = new BitVector();
            try
            {
                bv.rank(100, true);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException)
            {

            }
        }

        [TestMethod]
        public void rank() {
            ulong counter = 0;
            foreach (ulong v in values)
            {
                Assert.AreEqual(counter, bv1.rank(v, true));
                Assert.AreEqual(counter, bv0.rank(v, false));
                counter++;
            }
        }

        [TestMethod]
        public void select() {
            ulong counter = 0;
            foreach (ulong v in values)
            {
             
                Assert.AreEqual(v, bv1.select(counter, true));
                Assert.AreEqual(v, bv0.select(counter, false));
                counter++;
            }
        }

        [TestMethod]
        public void selectBeforeBuild()
        {
            BitVector bv = new BitVector();
            try
            {
                bv.select(100, true);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException)
            {

            }
        }

        [TestMethod]
        public void get_boundary()
        {
            try {
                bv1.get(bv1.size());
                Assert.Fail("bv1.get()");
            } catch (IndexOutOfRangeException) { 
            }
            try {
                bv0.get(bv0.size());
                Assert.Fail("bv0.get()");
            }
            catch (IndexOutOfRangeException) { 
            }
        }

        [TestMethod]
        public void rank_boundary() {
            try {
                bv1.rank(bv1.size() + 1, true);
                Assert.Fail("bv1.rank(true)");
            } catch (IndexOutOfRangeException) {
            }
            try {
                bv1.rank(bv1.size() + 1, false);
                Assert.Fail("bv1.rank(false)");
            } catch (IndexOutOfRangeException) {
            }
            try {
                bv1.rank(bv0.size() + 1, true);
                Assert.Fail("bv0.rank(true)");
            } catch (IndexOutOfRangeException) {
            }
            try {
                bv1.rank(bv0.size() + 1, false);
                Assert.Fail("bv0.rank(false)");
            } catch (IndexOutOfRangeException) {
            }
        }

        [TestMethod]
        public void select_boundary() {
            try {
                bv1.select(bv1.size(true), true);
                Assert.Fail("bv1.select(true)");
            }
            catch (ArgumentOutOfRangeException) { }
            try {
                bv1.select(bv1.size(false), false);
                Assert.Fail("bv1.select(false)");
            }
            catch (ArgumentOutOfRangeException) { }
            try {
                bv0.select(bv0.size(true), true);
                Assert.Fail("bv0.select(true)");
            }
            catch (ArgumentOutOfRangeException) { }
            try {
                bv0.select(bv0.size(false), false);
                Assert.Fail("bv0.select(false)");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void readAndWrite()
        {
            MemoryStream ms1 = new MemoryStream();
            BinaryWriter w1 = new BinaryWriter(ms1);
            bv1.write(w1);

            ms1.Seek(0, SeekOrigin.Begin);
            BitVector bv1Clone = new BitVector();
            bv1Clone.read(new BinaryReader(ms1));

            Assert.AreEqual(bv1.size(), bv1Clone.size());
            Assert.AreEqual(bv1.size(true), bv1Clone.size(true));
            Assert.AreEqual(bv1.size(false), bv1Clone.size(false));
            for (ulong i = 0; i < bv1.size(); i++)
            {
                Assert.AreEqual(bv1.get(i), bv1Clone.get(i));
            }

            MemoryStream ms0 = new MemoryStream();
            BinaryWriter w0 = new BinaryWriter(ms0);
            bv0.write(w0);

            ms0.Seek(0, SeekOrigin.Begin);
            BitVector bv0Clone = new BitVector();
            bv0Clone.read(new BinaryReader(ms0));

            Assert.AreEqual(bv0.size(), bv0Clone.size());
            Assert.AreEqual(bv0.size(true), bv0Clone.size(true));
            Assert.AreEqual(bv0.size(false), bv0Clone.size(false));
            for (ulong i = 0; i < bv0.size(); i++)
            {
                Assert.AreEqual(bv0.get(i), bv0Clone.get(i));
            }

        }

        [TestMethod]
        public void fromBits()
        {
            const ulong LEN = 10000;
            Bits bits = new Bits(LEN);
            Random rand = new Random();
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) != 0;
                bits.set(i, b);
            }

            BitVector bv = new BitVector(bits);
            Assert.AreEqual(bits.size(), bv.size());
            for (ulong i = 0; i < LEN; i++)
            {
                Assert.IsTrue(bits.get(i) == bv.get(i), string.Format("Faild at {0}.", i));
            }

        }
        [TestMethod]
        public void regressAll()
        {
            size();
            get();
            rankBeforeBuild();
            rank();
            selectBeforeBuild();
            select();
            get_boundary();
            rank_boundary();
            select_boundary();
            readAndWrite();
        }

    }
}
