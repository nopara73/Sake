using NBitcoin;
using Sake;
using WalletWasabi.Extensions;

var inputCount = 218;
var userCount = 42;
var remixRatio = 0.96;

var min = Money.Satoshis(5000m);
var max = Money.Coins(43000m);
var feeRate = new FeeRate(10m);
var random = new Random();

var maxInputCost = Money.Satoshis(Math.Max(NBitcoinExtensions.P2wpkhInputVirtualSize, NBitcoinExtensions.P2trInputVirtualSize) * feeRate.SatoshiPerByte);

// Don't select inputs that costs more to spend than their value. This is what happens in SelectCoinsForRound.
var preRandomAmounts = Sample.Amounts
    .Where(x => Money.Coins(x) > maxInputCost)
    .RandomElements(inputCount)
    .Select(x => new Input(Money.Coins(x), Mixer.GetNextScriptType(true, random), feeRate));

var preGroups = preRandomAmounts.RandomGroups(userCount);
var preMixer = new Mixer(feeRate, min, max, true, random);
var preMix = preMixer.CompleteMix(preGroups);

var remixCount = (int)(inputCount * remixRatio);

var randomAmounts = Sample.Amounts
    .Where(x => Money.Coins(x) > maxInputCost)
    .RandomElements(inputCount - remixCount)
    .Select(x => new Input(Money.Coins(x), Mixer.GetNextScriptType(true, random), feeRate));

var remixAmounts = preMix.SelectMany(x => x)
    .Where(x => Money.Satoshis(x) > maxInputCost)
    .RandomElements(remixCount)
    .Select(x => new Input(Money.Satoshis(x), Mixer.GetNextScriptType(true, random), feeRate));

var newRoundAmounts = randomAmounts.Concat(remixAmounts);
var newRoundInputGroups = newRoundAmounts.RandomGroups(userCount).ToArray();
var mixer = new Mixer(feeRate, min, max, true, random);
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

Console.WriteLine();
Console.WriteLine($"Number of users:\t{userCount}");
Console.WriteLine($"Number of inputs:\t{inputCount}");
Console.WriteLine($"Number of outputs:\t{outputCount}");
Console.WriteLine($"Number of changes:\t{changeCount:0}");
Console.WriteLine($"Total in:\t\t{inputAmount / 100000000m} BTC");
Console.WriteLine($"Fee paid for inputs:\t{feeInputs / 100000000m} BTC");
Console.WriteLine($"Fee paid for outputs:\t{feeOutputs / 100000000m} BTC");
Console.WriteLine($"Total fee:\t\t{(feeInputs + feeOutputs) / 100000000m} BTC");
Console.WriteLine($"Size:\t\t\t{size} vbyte");
Console.WriteLine($"Fee rate:\t\t{calculatedFeeRate.ToString("f2")} sats/vbyte");
Console.WriteLine($"Average anonset:\t{Analyzer.AverageAnonsetGain(newRoundInputGroups, outputGroups):0.##}");
Console.WriteLine($"Average input anonset:\t{Analyzer.AverageAnonsetGain(newRoundInputGroups):0.##}");
Console.WriteLine($"Average output anonset:\t{Analyzer.AverageAnonsetGain(outputGroups):0.##}");
Console.WriteLine($"Blockspace efficiency:\t{Analyzer.BlockspaceEfficiency(newRoundInputGroups, outputGroups, size):0.##}");
Console.WriteLine($"Total leftover:\t\t{mixer.Leftovers.Sum():0}");
Console.WriteLine($"Median leftover:\t{mixer.Leftovers.Median():0}");
Console.WriteLine($"Largest leftover:\t{mixer.Leftovers.Max():0}");
Console.WriteLine($"Taproot/bech32 ratio:\t{mixer.Outputs.Where(o => o.ScriptType == ScriptType.Taproot).Count()}/{mixer.Outputs.Where(o => o.ScriptType == ScriptType.P2WPKH).Count()}");
