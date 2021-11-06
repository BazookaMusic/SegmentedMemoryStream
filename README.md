# Segmented Memory Stream
![Build and Tests](https://github.com/BazookaMusic/ SegmentedMemoryStream/.github/workflows/dotnet-test.yml/badge.svg)

## What is it?
The segmented memory stream is a memory stream implementation which splits the underlying storage into smaller sub-arrays instead of using a contiguous array. This way it avoids causing LOH allocations as well as having to re-allocate all of the memory when it requires more space.
Instead the implementation uses a list of segments to store the bytes.


## When to use it?
If your application needs to use a memory stream but you have noticed LOH fragmentation.


## How well does it perform?
See comparison below. It is faster for writing but can be up to 2x slower to read for very large sizes.

|                      Method |          N |             Mean |            Error |           StdDev |       Gen 0 |      Gen 1 |       Allocated |
|---------------------------- |----------- |-----------------:|-----------------:|-----------------:|------------:|-----------:|----------------:|
|           Read_MemoryStream |      65536 |         982.4 ns |          1.62 ns |          1.51 ns |           - |          - |               - |
|  Read_SegmentedMemoryStream |      65536 |       1,091.4 ns |          0.64 ns |          0.59 ns |           - |          - |               - |
|          Write_MemoryStream |      65536 |       3,185.0 ns |         71.97 ns |        212.21 ns |      7.8087 |     0.7782 |        65,624 B |
| Write_SegmentedMemoryStream |      65536 |       1,166.3 ns |          1.35 ns |          1.26 ns |      0.0153 |          - |           136 B |
|           Read_MemoryStream |    1048576 |      24,064.3 ns |        475.89 ns |      1,245.31 ns |           - |          - |               - |
|  Read_SegmentedMemoryStream |    1048576 |      27,931.7 ns |        731.69 ns |      2,157.40 ns |           - |          - |               - |
|          Write_MemoryStream |    1048576 |     531,915.2 ns |      2,325.32 ns |      2,175.11 ns |      7.3242 |          - |     2,031,800 B |
| Write_SegmentedMemoryStream |    1048576 |      20,177.5 ns |         57.76 ns |         54.02 ns |      0.1831 |          - |         1,576 B |
|           Read_MemoryStream | 1073741824 |  71,148,375.2 ns |    179,377.45 ns |    167,789.77 ns |           - |          - |         1,261 B |
|  Read_SegmentedMemoryStream | 1073741824 | 144,784,821.4 ns |  2,262,594.60 ns |  2,005,731.18 ns |           - |          - |               - |
|          Write_MemoryStream | 1073741824 | 608,680,654.2 ns | 11,980,887.27 ns | 15,578,537.63 ns |           - |          - | 2,147,418,536 B |
| Write_SegmentedMemoryStream | 1073741824 | 523,974,784.6 ns |  7,229,573.88 ns |  6,037,021.62 ns | 131000.0000 | 47000.0000 | 1,728,751,720 B |