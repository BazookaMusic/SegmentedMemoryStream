namespace MemoryStreamBench
{
    using System;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;

    using Bazooka.SegmentedMemoryStream;
   

    [MemoryDiagnoser]
    public class SegmentedMemoryStreamBenchmarks
    {
        [Params(64 * 1024, 1024 * 1024, 1024 * 1024 * 1024)]
        public int N { get; set; }

        private static byte[] data = new byte[64 * 1024];
        private static byte[] buffer = new byte[1024*1024*1024];

        private static MemoryStream ms = new MemoryStream();

        private static SegmentedMemoryStream sms = new SegmentedMemoryStream();


        public SegmentedMemoryStreamBenchmarks()
        {
            for (int i = 0; i < 1024 * 1024 * 1024; i++)
            {
                ms.WriteByte(200);
                sms.WriteByte(200);
            }
        }

        [Benchmark]
        public byte[] Read_MemoryStream()
        {
            ms.Position = 0;
            ms.Read(buffer, 0, N);
            return buffer;
        }

        [Benchmark]
        public byte[] Read_SegmentedMemoryStream()
        {
            sms.Position = 0;
            sms.Read(buffer.AsSpan().Slice(0, N));
            return buffer;
        }

        [Benchmark]
        public void Write_MemoryStream()
        {
            var nms = new MemoryStream();

            while (nms.Length != N)
            {
                nms.Write(data, 0, Math.Min(N, data.Length));
            }
        }

        [Benchmark]
        public void Write_SegmentedMemoryStream()
        {
            using (var nsms = new SegmentedMemoryStream())
            {
                while (nsms.Length != N)
                {
                    nsms.Write(data.AsSpan().Slice(0, Math.Min(N, data.Length)));
                }
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            //BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
            BenchmarkDotNet.Reports.Summary summary1 = BenchmarkRunner.Run<SegmentedMemoryStreamBenchmarks>();

            Console.ReadKey();
        }
    }
}