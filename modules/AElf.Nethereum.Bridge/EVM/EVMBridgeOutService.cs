using System.Numerics;
using AElf.Nethereum.Core;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge.EVM;
public class EVMBridgeOutService : ContractServiceBase, IClientBridgeOutService, ITransientDependency
{
    protected override string SmartContractName { get; } = "BridgeOut";
    
    public ILogger<EVMBridgeOutService> Logger { get; set; }

    public async Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report,
        byte[][] rs, byte[][] ss, byte[] rawVs)
    {
        var setValueFunction = GetFunction(chainId, contractAddress, "transmit");
        var sender = GetAccount().Address;

        Logger.LogInformation($"Transmit sender: {sender}");
        
        var gas = await setValueFunction.EstimateGasAsync(sender, null, null, swapHashId, report, rs, ss, rawVs);
        gas.Value = BigInteger.Multiply(gas.Value, 2);
        Logger.LogInformation($"Transmit params: report:{report.ToHex()},rawVs:{rawVs.ToHex()}");
        foreach (var r in rs)
        {
            Logger.LogInformation($"Transmit params: rs:{r.ToHex()}");
        }
        foreach (var s in ss)
        {
            Logger.LogInformation($"Transmit params: rs:{s.ToHex()}");
        }
        
        var transactionResult =
            await setValueFunction.SendTransactionAsync(sender, gas, null, null, swapHashId, report,
                rs, ss, rawVs);
        return transactionResult;
    }

    public List<string> GetClientAliasList()
    {
        return GetChainIdList();
    }
}