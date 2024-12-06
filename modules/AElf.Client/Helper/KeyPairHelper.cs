using AElf.Cryptography;

namespace AElf.Client.Helper;

public static class KeyPairHelper
{
    public static string CreateKeyPair()
    {
        return CryptoHelper.GenerateKeyPair().PrivateKey.ToHex();
    }
}