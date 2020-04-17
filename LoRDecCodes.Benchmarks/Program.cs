using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using LoRDeckCodes;
using RunMode = BenchmarkDotNet.Jobs.RunMode;

namespace LoRDecCodes.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var benchmarks = new[]
            {
                typeof(DeckEncoderBench), typeof(Base32Bench), typeof(SorterBench), typeof(HeapVsStackBench),
                typeof(OrderIsImportantBench)
            };

            while (true)
            {
                for (int i = 0; i < benchmarks.Length; i++)
                {
                    Console.WriteLine($"[{i}]: {benchmarks[i].Name}");
                }

                if (!int.TryParse(Console.ReadLine(), out var selectedIndex))
                {
                    Console.Clear();
                    continue;
                }

                BenchmarkRunner.Run(benchmarks[selectedIndex]);
            }
        }
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [RankColumn]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class DeckEncoderBench
    {
        private readonly List<string> _codes = new List<string>();
        private readonly List<List<CardCodeAndCount>> _decks = new List<List<CardCodeAndCount>>();

        [Params(1,10, 100)]
        public int Iterations { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            //Load the test data from file.
            string line;
            using var myReader = new StreamReader("DeckCodesTestData.txt");

            while ((line = myReader.ReadLine()) != null)
            {
                _codes.Add(line);
                var newDeck = new List<CardCodeAndCount>();
                while (!string.IsNullOrEmpty(line = myReader.ReadLine()))
                {
                    var parts = line.Split(new[] {':'});
                    newDeck.Add(new CardCodeAndCount() {Count = int.Parse(parts[0]), CardCode = parts[1]});
                }

                _decks.Add(newDeck);
            }
        }

        [Benchmark(Description = "Riot:GetDeckFromCode", Baseline = true), BenchmarkCategory(nameof(LoRDeckEncoder.GetDeckFromCode))]
        public List<List<CardCodeAndCount>> RiotGetDeckFromCode()
        {
            var result = new List<List<CardCodeAndCount>>();

            for (var x = 0; x < Iterations; x++)
                foreach (var code in _codes)
                {
                    var deck = LoRDeckEncoder.GetDeckFromCode(code);
                    result.Add(deck);
                }

            return result;
        }

        [Benchmark(Description = "Riot:GetCodeFromDeck", Baseline = true), BenchmarkCategory(nameof(LoRDeckEncoder.GetCodeFromDeck))]
        public void RiotGetCodeFromDeck()
        {
            for (var x = 0; x < Iterations; x++)
                for (var i = 0; i < _codes.Count; i++)
                {
                    ThrowIfFalse(_codes[i] == LoRDeckEncoder.GetCodeFromDeck(_decks[i]));
                }
        }

        [Benchmark(Description = "CodeDux:GetDeckFromCode"), BenchmarkCategory(nameof(LoRDeckEncoder.GetDeckFromCode))]
        public List<List<CardCodeAndCount>> CodeDuxGetDeckFromCode()
        {
            var result = new List<List<CardCodeAndCount>>();
            for (var x = 0; x < Iterations; x++)
                foreach (var code in _codes)
                {
                    var deck = DeckEncoder.GetDeckFromCode(code);
                    result.Add(deck);
                }

            return result;
        }

        [Benchmark(Description = "CodeDux:GetCodeFromDeck"), BenchmarkCategory(nameof(LoRDeckEncoder.GetCodeFromDeck))]
        public void CodeDuxGetCodeFromDeck()
        {
            for (var x = 0; x < Iterations; x++)
                for (var i = 0; i < _codes.Count; i++)
                {
                    ThrowIfFalse(_codes[i] == DeckEncoder.GetCodeFromDeck(_decks[i]));
                }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowIfFalse(bool b)
        {
            if(!b)
                throw new Exception();
        }
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [RankColumn]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class Base32Bench
    {
        private readonly byte[] _decoded;
        private readonly string _encoded;

        public Base32Bench()
        {
            _encoded = File.ReadAllText("Lorem.Base32.txt");
            _decoded = File.ReadAllBytes("Lorem.txt");

            var encodeEqual = Base32Span.EncodeNoAlloc(_decoded).SequenceEqual(Base32.Encode(_decoded));
            if (!encodeEqual)
                throw new Exception("Encode is not fair");

            var decodeEqual = Base32Span.DecodeNoAlloc(_encoded).SequenceEqual(Base32.Decode(_encoded));
            if(!decodeEqual)
                throw new Exception("Decode is not fair");
        }

        [Params(1, 10, 100)]
        public int Iterations { get; set; }

        [Benchmark(Baseline = true), BenchmarkCategory(nameof(Base32.Decode))]
        public int Base32Decode()
        {
            var result = 0;

            for (var i = 0; i < Iterations; i++)
                result += Base32.Decode(_encoded).Length;

            return result;
        }

        [Benchmark, BenchmarkCategory(nameof(Base32.Decode))]
        public int Base32SpanDecode()
        {
            var result = 0;

            for (var i = 0; i < Iterations; i++)
                result += Base32Span.DecodeNoAlloc(_encoded).Length;

            return result;
        }

        [Benchmark(Baseline = true), BenchmarkCategory(nameof(Base32.Encode))]
        public int Base32Encode()
        {
            var result = 0;

            for (var i = 0; i < Iterations; i++)
                result += Base32.Encode(_decoded).Length;

            return result;
        }

        [Benchmark, BenchmarkCategory(nameof(Base32.Encode))]
        public int Base32SpanEncode()
        {
            var result = 0;

            for (var i = 0; i < Iterations; i++)
                result += Base32Span.EncodeNoAlloc(_decoded).Length;

            return result;
        }
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [RankColumn]
    [CategoriesColumn]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class SorterBench
    {
        private class Item
        {
            public Item(int id, int weight)
            {
                Id = id;
                Weight = weight;
            }

            public int Id { get; }
            public int Weight { get; }
        }

        private Item[] _randoms;
        private readonly int[] _idsToFind = {500, 100, 900};

        [GlobalSetup]
        public void Setup()
        {
            _randoms = new Item[1000];
            var random = new Random(42);
            for (int i = 0; i < _randoms.Length; i++)
            {
                var weight = random.Next();
                _randoms[i] = new Item(i, weight);
            }

            var sortedList = _randoms.ToList();
            sortedList.Sort((x, y) => x.Weight.CompareTo(y.Weight));
            var sortEqualsOrderBy = _randoms.OrderBy(r => r.Weight).SequenceEqual(sortedList);

            if(!sortEqualsOrderBy)
                throw new Exception("Sort isn't fair");
        }

        [Benchmark, BenchmarkCategory("Sort")]
        public int OrderByToList()
        {
            var randomList = _randoms.ToList();
            var sorted = randomList.OrderBy(r => r.Weight).ToList();
            return sorted[0].Id;
        }


        [Benchmark, BenchmarkCategory("Sort")]
        public int OrderByToArray()
        {
            var randomList = _randoms.ToList();
            var sorted = randomList.OrderBy(r => r.Weight).ToArray();
            return sorted[0].Id;
        }

        [Benchmark, BenchmarkCategory("Sort")]
        public int OrderBy()
        {
            var randomList = _randoms.ToList();
            var sorted = randomList.OrderBy(r => r.Weight);
            return sorted.First().Id;
        }

        [Benchmark, BenchmarkCategory("Sort")]
        public int Sort()
        {
            var randomList = _randoms.ToList();
            randomList.Sort((x, y) => x.Weight.CompareTo(y.Weight));
            return randomList[0].Id;
        }


        [Benchmark, BenchmarkCategory("Find")]
        public int FirstOrDefault()
        {
            var randomList = _randoms.ToList();
            var result = 0;
            for (int i = 0; i < _idsToFind.Length; i++)
            {
                var id = _idsToFind[i];
                result += randomList.First(r => r.Id == id).Id;
            }

            return result;
        }

        [Benchmark, BenchmarkCategory("Find")]
        public int Find()
        {
            var randomList = _randoms.ToList();
            var result = 0;
            for (int i = 0; i < _idsToFind.Length; i++)
            {
                var id = _idsToFind[i];
                result += randomList.Find(r => r.Id == id).Id;
            }

            return result;
        }
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [RankColumn]
    public class HeapVsStackBench
    {
        [Benchmark]
        public int HeapArray()
        {
            Span<int> data = new int[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = i;
            }

            return Count(in data);
        }

        [Benchmark]
        public int StackArray()
        {
            Span<int> data = stackalloc int[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = i;
            }

            return Count(in data);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Count(in Span<int> input) => input.Length;
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [MemoryDiagnoser]
    [RankColumn]
    public class OrderIsImportantBench
    {
        private class Item
        {
            public int Age { get; set; } = 33;
            public string Name { get; set; } = "RandomName";
            public string[] Roles { get; set; } = {"Customer"};
        }

        public class ItemDto
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public bool IsVip { get; set; }
        }

        private List<Item> _items;

        [GlobalSetup]
        public void Setup()
        {
            _items = new List<Item>();
            for (int i = 0; i < 1000; i++)
            {
                _items.Add(new Item());
            }

            _items[502] = new Item
            {
                Age = 22,
                Roles = new[] {"Vip", "Customer"}
            };
        }

        [Benchmark]
        public ItemDto A()
        {
            return _items.Select(i => new ItemDto
                {
                    Name = i.Name,
                    Age = i.Age,
                    IsVip = i.Roles.Contains("Vip")
                })
                .First(i => i.Age == 22);
        }

        [Benchmark]
        public ItemDto B()
        {
            return _items.Where(i => i.Age == 22).Select(i => new ItemDto
            {
                Name = i.Name,
                Age = i.Age,
                IsVip = i.Roles.Contains("Vip")
            }).First();
        }
    }
}