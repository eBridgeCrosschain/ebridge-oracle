using AElf.Client.Helper;

namespace AElf.Client.Extensions;

public static class StringExtensions
{
    public static Address ToAddress(this string? address)
    {
        if (address == null)
        {
            return Address.FromPublicKey(ByteArrayHelper.HexStringToByteArray(KeyPairHelper.CreateKeyPair()));
        }

        return Address.FromBase58(address);
    }
}