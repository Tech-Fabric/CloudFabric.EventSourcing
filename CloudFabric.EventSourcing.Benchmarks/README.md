``` ini

BenchmarkDotNet=v0.13.1, OS=fedora 36
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.103
  [Host]   : .NET 6.0.3 (6.0.322.16001), X64 RyuJIT
  QuickJob : .NET 6.0.3 (6.0.322.16001), X64 RyuJIT

Job=QuickJob  InvocationCount=100  IterationCount=20  
LaunchCount=10  UnrollFactor=1  WarmupCount=1  

```

|                Method |        Mean |     Error |      StdDev |      Median |
|---------------------- |------------:|----------:|------------:|------------:|
|   CreateOrderInMemory |    250.0 μs |  11.15 μs |    46.47 μs |    234.2 μs |
|   CreateOrderCosmosDb | 90,631.4 μs | 405.06 μs | 1,706.20 μs | 90,466.3 μs |
| CreateOrderPostgresql | 12,813.8 μs | 775.26 μs | 3,282.51 μs | 12,991.5 μs |

```
sudo docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254  -m 16g --cpus=12.0 --name=linux-emulator-fiber-eventstore -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=false -e AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=$ipaddr -it mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```