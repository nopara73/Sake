namespace Sake;

public class SimulationResult
{
    public int UserCount { get; }
    public int InputCount { get; }
    public int DiffInputNumber { get; }
    public int OutputCount { get; }
    public int ChangeCount { get; }
    public ulong InputAmount { get; }
    public ulong FeeInputs { get; }
    public ulong FeeOutputs { get; }
    public ulong TotalFee { get; }
    public int Size { get; }
    public decimal CalculatedFeeRate { get; }
    public decimal AverageAnonset { get; }
    public decimal AverageInputAnonset { get; }
    public decimal AverageOutputAnonset { get; }
    public decimal BlockSpaceEfficiency { get; }
    public int TotalLeftover { get; }
    public double? MedianLeftover { get; }
    public int LargestLeftover { get; }
    public int TaprootCount { get; }
    public int Bech32Count { get; }

    public SimulationResult(
        int userCount,
        int inputCount,
        int diffInputNumber,
        int outputCount,
        int changeCount,
        ulong inputAmount,
        ulong feeInputs,
        ulong feeOutputs,
        ulong totalFee,
        int size,
        decimal calculatedFeeRate,
        decimal averageAnonset,
        decimal averageInputAnonset,
        decimal averageOutputAnonset,
        decimal blockSpaceEfficiency,
        int totalLeftover,
        double? medianLeftover,
        int largestLeftover,
        int taprootCount,
        int bech32Count
    )
    {
        UserCount = userCount;
        InputCount = inputCount;
        OutputCount = outputCount;
        ChangeCount = changeCount;
        InputAmount = inputAmount;
        DiffInputNumber = diffInputNumber;
        FeeInputs = feeInputs;
        FeeOutputs = feeOutputs;
        TotalFee = totalFee;
        Size = size;
        CalculatedFeeRate = calculatedFeeRate;
        AverageAnonset = averageAnonset;
        AverageInputAnonset = averageInputAnonset;
        AverageOutputAnonset = averageOutputAnonset;
        BlockSpaceEfficiency = blockSpaceEfficiency;
        TotalLeftover = totalLeftover;
        MedianLeftover = medianLeftover;
        LargestLeftover = largestLeftover;
        TaprootCount = taprootCount;
        Bech32Count = bech32Count;
    }
}
