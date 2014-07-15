CSBitVectors
============

This is:
---
a C# implementation of data structures known as _succinct bit vectors_.

A _Succinct Bit Vector_ is:
---
a bit vector augmented with an "index", such that
- the size of the index is "insignificant" compared to that of the vector itself,
- the index is exploited to answer the following queries in sublinear time:
  1. _rank_: returns the number of 0s or 1s within a given range of the vector   
```
  rank : BitVector -> Nat -> {0, 1} -> Nat
  rank vec k b = |{i | i <- [0, ..., k) vec[i] = b }|
```
  (N.B. The above claims the specified range is _exclusive_: `i<- [0, ..., k)`,
   some formalisms instead employ _inclusive_ definition for a range: `i <- [0, ..., k]`)
  2. _select_: returns the position of the _k_-th occurrence of 0 or 1: 
````
  select : BitVector -> Nat -> {0, 1} -> Nat
  select vec k b = i such that vec[i] = b && rank vec (i+1) b = k
````

This Implementation Offers:
---
two distinct representation of bit vectors:

- `BitVector` stores an _uncompressed_ bit sequence with its index, and
- `RRRBitVector` stores a _compressed_ bits with index, a la Raman, Raman and Rao, 2002.

Usage:
---
For both representations, an instance is built from a plain-vanilla bit sequence
stored in the helper class `Bits`:

1. Load your bulk data into a list of bytes: 
```cs
IList<byte> bulk = /* ...my precious... */;
```
2. Instantiate a `Bits` and copy the bulk:
```cs
Bits bits = new Bits(bulk.Length / 8 /* is the initial capacity */);
bits.push(bulk);
```

3. Build a bit vector from `bits`:
```cs
(RRR)BitVector bv = new (RRR)BitVector(bits);
```

Supported operations are:

- `rank()/select()` as described above
- `get()` for random access to an arbitrary bit (N.B. random access over a _compressed_ vector is _slow_)
- `write()/read()` for serialization/de-serialization, respectively

See the interface `IBitVector` for the complete list.

Supplements:
---
Two helper classes are supplied besides:

- `Bits`: is, as introduced above, a container for plain-vanilla bit sequences.
  A sequence can be of an arbitrary length (i.e. not necessarily be a multiple of 8, 16, etc.)
- `EliasFanoSequence`: is another instance of "succinct data structures" to encode a
  _non-decreasing_ _finite_ _sequence_ _of_ _natural_ _numbers_ in a "space efficient" way.
  It is used as a part of compressed vector representation, but might be applied for other purposes in your code.

References:
---
Googling "succinct bit vector" or "succinct data structures" will give you a bunch of
well-organized introductory articles.

Specific publications and softwares which my implementation based upon are
listed in each source code.

License:
---
MIT License.

