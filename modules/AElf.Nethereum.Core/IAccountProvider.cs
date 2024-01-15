namespace AElf.Nethereum.Core;

public interface IAccountProvider<out T>
{
    T GetAccount(string alias);
}