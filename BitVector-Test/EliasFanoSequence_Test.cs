using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitVectors;
using System.Collections.Generic;
using System.IO;

namespace BitVector_Test
{
    [TestClass]
    public class EliasFanoSequence_Test
    {
        [TestMethod]
        public void setAndGet()
        {
            const ulong U = 100 * 10000;
            EliasFanoSequence seq = new EliasFanoSequence(10000, U);
            Random rand = new Random();
            ulong v = (ulong)rand.Next() % 100;
            List<ulong> l = new List<ulong>(10000);
            for (int i = 0; i < 10000; i++)
            {
                seq.push(v);
                l.Add(v);
                v = v + ((ulong)rand.Next() % 100);
            }
            try
            {
                seq.push(v + 1);
                Assert.Fail();
            }
            catch (InvalidOperationException)
            {

            }

            seq.build();

            for (int i = 0; i < 10000; i++)
            {
                Assert.AreEqual(seq.get(i), l[i]);
            }
            try
            {
                seq.get(10000);
                Assert.Fail();
            }
            catch (ArgumentOutOfRangeException)
            {

            }
        }
        [TestMethod]
        public void readAndWrite()
        {
            MemoryStream ms1 = new MemoryStream();
            BinaryWriter w1 = new BinaryWriter(ms1);

            const ulong ITER = 100000;
            const ulong U = 100 * ITER;
            EliasFanoSequence seq = new EliasFanoSequence(ITER, U);
            Random rand = new Random();
            ulong v = (ulong)rand.Next() % 100;
            List<ulong> l = new List<ulong>((int)ITER);
            for (ulong i = 0; i < ITER; i++)
            {
                seq.push(v);
                l.Add(v);
                v = v + ((ulong)rand.Next() % 100);
            }
            seq.build();


            seq.write(w1);

            Console.WriteLine(string.Format("The length of randomely generated monotone sequence of length {0} = {1} bytes", seq.size(), ms1.Length));

            ms1.Seek(0, SeekOrigin.Begin);
            EliasFanoSequence seq2 = new EliasFanoSequence(seq.maxLength(), seq.upperBound());
            seq2.read(new BinaryReader(ms1));

            Assert.AreEqual(seq.size(), seq2.size());
            for (ulong i = 0; i < seq.size(); i++)
            {
                Assert.AreEqual(seq.get(i), seq2.get(i));
            }

            Assert.AreEqual(seq, seq2);

        }
    }
}
