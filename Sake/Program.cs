﻿using NBitcoin;
using Sake;
using WalletWasabi.Extensions;

var results = new List<SimulationResult>();

for (int i = 0; i < 100; i++)
{
    var inputCount = 218;
    var userCount = 42;
    var remixRatio = 0.96;
    var allowedOutputTypes = new List<ScriptType> { ScriptType.Taproot, ScriptType.P2WPKH };

    var min = Money.Satoshis(5000m);
    var max = Money.Coins(43000m);
    var feeRate = new FeeRate(200m);
    var random = new Random();

    var maxInputCost = Money.Satoshis(Math.Max(NBitcoinExtensions.P2wpkhInputVirtualSize, NBitcoinExtensions.P2trInputVirtualSize) * feeRate.SatoshiPerByte);

    // Don't select inputs that costs more to spend than their value. This is what happens in SelectCoinsForRound.
    var preRandomAmounts = Sample.Amounts
        .Where(x => Money.Coins(x) > maxInputCost)
        .RandomElements(inputCount)
        .Select(x => new Input(Money.Coins(x), allowedOutputTypes.RandomElement(random), feeRate));


    var preMixer = new Mixer(feeRate, min, max, allowedOutputTypes, random);

    var preGroups = preMixer.RandomInputGroups(preRandomAmounts, userCount);
    var preMix = preMixer.CompleteMix(preGroups).ToArray();

    var remixCount = (int)(inputCount * remixRatio);

    var randomAmounts = Sample.Amounts
        .Where(x => Money.Coins(x) > maxInputCost)
        .RandomElements(inputCount - remixCount)
        .Select(x => new Input(Money.Coins(x), allowedOutputTypes.RandomElement(random), feeRate));

    var nbMissingOutputsForRemixRate = Math.Max(0, remixCount - preMix.SelectMany(x => x).Count(x => Money.Satoshis(x) > maxInputCost));

    if (nbMissingOutputsForRemixRate > 0)
    {
        while (preMix.SelectMany(x => x).Count(x => Money.Satoshis(x) > maxInputCost) < remixCount)
        {
            preMix = preMix.Concat(preMix).ToArray();
        }
    }

    var remixAmounts = preMix.SelectMany(x => x)
        .Where(x => Money.Satoshis(x) > maxInputCost)
        .RandomElements(remixCount)
        .Select(x => new Input(Money.Satoshis(x), allowedOutputTypes.RandomElement(random), feeRate));

    var newRoundAmounts = randomAmounts.Concat(remixAmounts);
    var mixer = new Mixer(feeRate, min, max, allowedOutputTypes, random);
    var newRoundInputGroups = mixer.RandomInputGroups(newRoundAmounts, userCount).ToArray();
    var outputGroups = mixer.CompleteMix(newRoundInputGroups).Select(x => x.ToArray()).ToArray();

    if ((ulong)newRoundInputGroups.SelectMany(x => x).Sum(x => x.EffectiveValue) <= outputGroups.SelectMany(x => x).Sum())
    {
        throw new InvalidOperationException("Bug. Transaction doesn't pay fees.");
    }

    var outputCount = outputGroups.Sum(x => x.Length);
    var inputAmount = (ulong)newRoundInputGroups.SelectMany(x => x).Sum(x => x.Amount);
    var inputEffectiveAmount = (ulong)newRoundInputGroups.SelectMany(x => x).Sum(x => x.EffectiveValue);
    var outputAmount = outputGroups.SelectMany(x => x).Sum();
    var changeCount = outputGroups.SelectMany(x => x).GetIndistinguishable(includeSingle: true).Count(x => x.count == 1);
    var feeInputs = inputAmount - inputEffectiveAmount;
    var feeOutputs = inputEffectiveAmount - outputAmount;
    var size = newRoundInputGroups.SelectMany(x => x).Sum(x => x.ScriptType.EstimateInputVsize()) + mixer.Outputs.Sum(o => o.ScriptType.EstimateOutputVsize());
    var calculatedFeeRate = (feeInputs + feeOutputs) / (decimal)size;

    Console.WriteLine();

    foreach (var (value, count, unique) in newRoundInputGroups.Select(x => x.Select(y => y.Amount.Satoshi))
        .GetIndistinguishable()
        .OrderBy(x => x.value))
    {
        if (count == 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        var displayResult = count.ToString();
        if (count != unique)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            displayResult = $"{unique}/{count} unique/total";
        }
        Console.WriteLine($"There are {displayResult} occurrences of\t{value / 100000000m} BTC input.");
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    Console.WriteLine();

    foreach (var (value, count, unique) in outputGroups
        .GetIndistinguishable()
        .OrderBy(x => x.value))
    {
        if (count == 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
        }
        var displayResult = count.ToString();
        if (count != unique)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            displayResult = $"{unique}/{count} unique/total";
        }
        Console.WriteLine($"There are {displayResult} occurrences of\t{value / 100000000m} BTC output.");
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    var result = new SimulationResult(
        newRoundInputGroups.Length,
        newRoundInputGroups.SelectMany(x => x).Count(),
        nbMissingOutputsForRemixRate,
        outputCount,
        changeCount,
        inputAmount,
        feeInputs,
        feeOutputs,
        feeInputs + feeOutputs,
        size,
        calculatedFeeRate,
        Analyzer.AverageAnonsetGain(newRoundInputGroups, outputGroups),
        Analyzer.AverageAnonsetGain(newRoundInputGroups),
        Analyzer.AverageAnonsetGain(outputGroups),
        Analyzer.BlockspaceEfficiency(newRoundInputGroups, outputGroups, size),
        mixer.Leftovers.Sum(),
        mixer.Leftovers.Median(),
        mixer.Leftovers.Max(),
        mixer.Outputs.Count(o => o.ScriptType == ScriptType.Taproot),
        mixer.Outputs.Count(o => o.ScriptType == ScriptType.P2WPKH)
    );
    results.Add(result);
    Display.DisplayResults(new List<SimulationResult>() { result });
}
Display.DisplayResults(results);