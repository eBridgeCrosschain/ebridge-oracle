using AElf.Nethereum.Core;
using AElf.Nethereum.Core.Tron;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using TronClient;
using Volo.Abp.DependencyInjection;

namespace AElf.Nethereum.Bridge.Tron;
public class TronBridgeOutService : IClientBridgeOutService, ITransientDependency
{
    public ITronClientProvider TronClientProvider { get; set; }
    public ITronAccountProvider TronAccountProvider { get; set; }
    public ILogger<TronBridgeOutService> Logger { get; set; }

    public async Task<string> TransmitAsync(string chainId, string contractAddress, byte[] swapHashId, byte[] report,
        byte[][] rs, byte[][] ss, byte[] rawVs)
    {
        var tronClient = TronClientProvider.GetClient(chainId);
        var contract = tronClient.GetContract(contractAddress);
        var wallet = TronAccountProvider.GetAccount("tron");
        
        Logger.LogInformation($"Transmit sender: {wallet.Address}");

        var transmitFunctionMessage = new TransmitFunctionMessage
        {
            SwapHashId = swapHashId,
            Report = report,
            Rs = rs,
            Ss = ss,
            RawVs = rawVs
        };
        
        var energyUsed = await contract.GetEnergyUsed(new TronConstantContractFunctionMessage<TransmitFunctionMessage>
        {
            FunctionMessage = transmitFunctionMessage,
            Visible = true
        });
        var price = await tronClient.GetLatestEnergyPrice();
        var feeLimit = energyUsed * price * 2;
        
        Logger.LogInformation($"Transmit params: report:{report.ToHex()},rawVs:{rawVs.ToHex()} feeLimit:{feeLimit}");
        foreach (var r in rs)
        {
            Logger.LogInformation($"Transmit params: rs:{r.ToHex()}");
        }
        foreach (var s in ss)
        {
            Logger.LogInformation($"Transmit params: rs:{s.ToHex()}");
        }
        
        var response = await contract.SendAsync(wallet, new TronSmartContractFunctionMessage<TransmitFunctionMessage>
        {
            FunctionMessage = transmitFunctionMessage,
            FeeLimit = feeLimit,
            CallValue = 0,
            CallTokenValue = 0,
            TokenId = 0,
            Visible = true
        });

        return response.txid;
    }

    public List<string> GetClientAliasList()
    {
        return TronClientProvider.GetClientAliasList();
    }
}