namespace ATSync.Core.P2P;

/// <summary>Base58 (estilo Bitcoin) — usado para codificar PeerIDs libp2p.</summary>
internal static class Base58
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>
    /// Codifica un array de bytes a base58btc. Basado en la implementación de referencia de Bitcoin.
    /// </summary>
    public static string Encode(byte[] data)
    {
        if (data.Length == 0) return "";

        // cuenta ceros iniciales — cada uno se traduce a un "1" (primer char del alfabeto)
        int zeros = 0;
        while (zeros < data.Length && data[zeros] == 0) zeros++;

        // tamaño de la representación base58 ≈ log(256)/log(58) * len
        var b58 = new int[(data.Length * 138 / 100) + 1];
        int length = 0;
        for (int i = zeros; i < data.Length; i++)
        {
            int carry = data[i];
            for (int j = 0; j < b58.Length && carry != 0; j++)
            {
                carry += b58[j] << 8;
                b58[j] = carry % 58;
                carry /= 58;
                if (j + 1 > length) length = j + 1;
            }
        }

        // saltar ceros al inicio del array b58
        int it = 0;
        while (it < b58.Length && b58[it] == 0) it++;

        // longitud final = zeros (de data) + (length - it) (de b58)
        var chars = new char[zeros + (length - it)];
        int idx = 0;
        for (int i = 0; i < zeros; i++) chars[idx++] = Alphabet[0];
        for (int i = it; i < length; i++)
            chars[idx++] = Alphabet[b58[length - 1 - (i - it)]];
        return new string(chars);
    }
}