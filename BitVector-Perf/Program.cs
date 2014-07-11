using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BitVectors;

namespace BitVector_Perf
{
    class Program
    {
        public static void perfTestBV()
        {
            const ulong LEN = 1000 * 1000 * 100;
            Stopwatch w = new Stopwatch();
            Random rand = new Random();
            BitVector bv = new BitVector();
            w.Start();
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) == 1;
                bv.set(i, b);
            }
            w.Stop();
            Console.WriteLine("BitVector.set(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, LEN);

            w = new Stopwatch();
            w.Start();
            bv.build();
            w.Stop();
            Console.WriteLine("BitVector.build(): took {0} msec.", w.ElapsedMilliseconds);

            const ulong ITER = 1000 * 1000;
            ulong size = bv.size(true);
            w = new Stopwatch();
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size;
                bv.get(r);
            }
            w.Stop();
            Console.WriteLine("BitVector.get(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            w = new Stopwatch();
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size;
                bv.select(r, true);
            }
            w.Stop();
            Console.WriteLine("BitVector.select(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            w = new Stopwatch();
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size;
                bv.rank(r, true);
            }
            w.Stop();
            Console.WriteLine("BitVector.rank(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            bv.write(writer);
            Console.WriteLine("BitVectors.write(): {0:#,##0} bytes for the vector of length {1:#,##0}", ms.Length, bv.size());

        }

        public static void perfTestRRR()
        {
            const ulong LEN = 1000 * 1000 * 100;
            Stopwatch w = new Stopwatch();
            Random rand = new Random();
            Bits bits = new Bits(LEN);
            for (ulong i = 0; i < LEN; i++)
            {
                bool b = (rand.Next() % 2) == 1;
                bits.set(i, b);
            }
            w.Start();
            RRRBitVector rrr = new RRRBitVector(bits);
            w.Stop();
            Console.WriteLine("RRRBitVector.constructor(Bits): took {0} msec. for a length-{1:#,##0} bit-vector", w.ElapsedMilliseconds, LEN);

            const ulong ITER = 1000 * 1000;
            ulong size = rrr.size(true);
            w = new Stopwatch();
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size;
                rrr.get(r);
            }
            w.Stop();
            Console.WriteLine("RRRBitVector.get(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            w = new Stopwatch();
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size;
                rrr.select(r, true);
            }
            w.Stop();
            Console.WriteLine("RRRBitVector.select(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            w = new Stopwatch();
            ulong size1 = rrr.size(true);
            w.Start();
            for (ulong i = 0; i < ITER; i++)
            {
                ulong r = (ulong)rand.Next() % size1;
                rrr.rank(r, true);
            }
            w.Stop();
            Console.WriteLine("RRRBitVector.rank(): took {0} msec. for {1:#,##0} times of iteration (including raondom-number generation)", w.ElapsedMilliseconds, ITER);

            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            rrr.write(writer);
            Console.WriteLine("RRRBitVector.write(): {0:#,##0} bytes for the vector of length {1:#,##0}", ms.Length, rrr.size());

        }

        static void Main(string[] args)
        {
            perfTestBV();
            perfTestRRR();
        }
    }
}
