using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using LoRDeckCodes;

namespace LoRDecCodes.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<GetDeckFromCode>();
        }
    }

    [SimpleJob(RuntimeMoniker.NetCoreApp30)]
    [RPlotExporter]
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [RankColumn]
    public class GetDeckFromCode
    {
        readonly List<string> _codes = new List<string>();
        readonly List<List<CardCodeAndCount>> _decks = new List<List<CardCodeAndCount>>();

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
            for (var i = 0; i < _codes.Count; i++)
            {
                ThrowIfFalse(_codes[i] == LoRDeckEncoder.GetCodeFromDeck(_decks[i]));
            }
        }

        [Benchmark(Description = "CodeDux:GetDeckFromCode"), BenchmarkCategory(nameof(LoRDeckEncoder.GetDeckFromCode))]
        public List<List<CardCodeAndCount>> CodeDuxGetDeckFromCode()
        {
            var result = new List<List<CardCodeAndCount>>();
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
            for (var i = 0; i < _decks.Count; i++)
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
}