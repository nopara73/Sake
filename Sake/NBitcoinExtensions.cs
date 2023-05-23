using NBitcoin;
namespace WalletWasabi.Extensions;

public static class NBitcoinExtensions
{

    public const int P2wpkhInputSizeInBytes = 41;
    public const int P2wpkhInputVirtualSize = 69;
    public const int P2pkhInputSizeInBytes = 145;
    public const int P2wpkhOutputVirtualSize = 31;

    public const int P2trInputVirtualSize = 58;
    public const int P2trOutputVirtualSize = 43;

    public static int EstimateOutputVsize(this Script scriptPubKey) =>
        new TxOut(Money.Zero, scriptPubKey).GetSerializedSize();

    public static int EstimateInputVsize(this Script scriptPubKey) =>
        scriptPubKey.GetScriptType().EstimateInputVsize();

    public static int EstimateInputVsize(this ScriptType scriptType) =>
        scriptType switch
        {
            ScriptType.P2WPKH => P2wpkhInputVirtualSize,
            ScriptType.Taproot => P2trInputVirtualSize,
            _ => throw new NotImplementedException($"Size estimation isn't implemented for provided script type.")
        };

    public static int EstimateOutputVsize(this ScriptType scriptType) =>
        scriptType switch
        {
            ScriptType.P2WPKH => P2wpkhOutputVirtualSize,
            ScriptType.Taproot => P2trOutputVirtualSize,
            _ => throw new NotImplementedException($"Size estimation isn't implemented for provided script type.")
        };

    public static Money EffectiveCost(this TxOut output, FeeRate feeRate) =>
        output.Value + feeRate.GetFee(output.ScriptPubKey.EstimateOutputVsize());

    public static Money EffectiveValue(this ICoin coin, FeeRate feeRate)
        => EffectiveValue(coin.TxOut.Value, virtualSize: coin.TxOut.ScriptPubKey.EstimateInputVsize(), feeRate);


    private static Money EffectiveValue(Money amount, int virtualSize, FeeRate feeRate)
    {
        var netFee = feeRate.GetFee(virtualSize);

        return amount - netFee;
    }

    public static ScriptType GetNextScriptType(bool isTaprootAllowed, Random random)
    {
        if (!isTaprootAllowed)
        {
            return ScriptType.P2WPKH;
        }

        return random.NextDouble() < 0.5 ? ScriptType.P2WPKH : ScriptType.Taproot;
    }

    public static ScriptType GetScriptType(this Script script)
    {
        return TryGetScriptType(script) ?? throw new NotImplementedException($"Unsupported script type.");
    }

    public static ScriptType? TryGetScriptType(this Script script)
    {
        foreach (ScriptType scriptType in new ScriptType[] { ScriptType.P2WPKH, ScriptType.P2PKH, ScriptType.P2PK, ScriptType.Taproot })
        {
            if (script.IsScriptType(scriptType))
            {
                return scriptType;
            }
        }

        return null;
    }

}