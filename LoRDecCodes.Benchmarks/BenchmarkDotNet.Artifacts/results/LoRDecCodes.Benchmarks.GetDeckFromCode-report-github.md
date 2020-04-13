``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.17763.1098 (1809/October2018Update/Redstone5)
Intel Core i7-8700K CPU 3.70GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  Job-MJEZDQ : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT

Runtime=.NET Core 3.0  

```
|                  Method |      Mean |    Error |   StdDev |    Median | Ratio | Rank |   Gen 0 |  Gen 1 | Gen 2 | Allocated |
|------------------------ |----------:|---------:|---------:|----------:|------:|-----:|--------:|-------:|------:|----------:|
|    Riot:GetDeckFromCode | 148.36 us | 0.406 us | 0.380 us | 148.37 us |  1.00 |    2 | 11.9629 | 1.9531 |     - |  73.66 KB |
| CodeDux:GetDeckFromCode |  53.61 us | 1.009 us | 1.036 us |  54.15 us |  0.36 |    1 |  9.5215 | 1.8311 |     - |  58.33 KB |
|                         |           |          |          |           |       |      |         |        |       |           |
|    Riot:GetCodeFromDeck | 236.68 us | 4.695 us | 5.407 us | 240.17 us |  1.00 |    2 | 37.1094 |      - |     - | 228.41 KB |
| CodeDux:GetCodeFromDeck | 120.84 us | 0.364 us | 0.284 us | 120.93 us |  0.51 |    1 | 11.3525 |      - |     - |  69.83 KB |
