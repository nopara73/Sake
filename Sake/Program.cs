using NBitcoin;
using Sake;

var inputCount = 100;
var userCount = 30;
var remixRatio = 0.3;

var preRandomAmounts = Sample.Amounts.RandomElements(inputCount).Select(x => x.ToSats());
var preGroups = preRandomAmounts.RandomGroups(userCount);

var min = Money.Satoshis(5000m);
var max = Money.Coins(43000m);
var feeRate = new FeeRate(10m);
var availableVSize = 255;
var random = new Random();

var preMixer = new Mixer(feeRate, min, max, availableVSize, true, random);
var preMix = preMixer.CompleteMix(preGroups);

var remixCount = (int)(inputCount * remixRatio);
var randomAmounts = Sample.Amounts.RandomElements(inputCount - remixCount).Select(x => x.ToSats()).Concat(preMix.SelectMany(x => x).RandomElements(remixCount));
var inputGroups = randomAmounts.RandomGroups(userCount).ToArray();
var mixer = new Mixer(feeRate, min, max, availableVSize, true, random);
var outputGroups = mixer.CompleteMix(inputGroups).Select(x => x.ToArray()).ToArray();

if (inputGroups.SelectMany(x => x).Sum() <= outputGroups.SelectMany(x => x).Sum())
{
    throw new InvalidOperationException("Bug. Transaction doesn't pay fees.");
}

var outputCount = outputGroups.Sum(x => x.Length);
var inputAmount = inputGroups.SelectMany(x => x).Sum();
var outputAmount = outputGroups.SelectMany(x => x).Sum();
var changeCount = outputGroups.SelectMany(x => x).GetIndistinguishable(includeSingle: true).Count(x => x.count == 1);
var fee = inputAmount - outputAmount;
var size = inputCount * mixer.InputSize + outputCount * mixer.OutputSize;
var calculatedFeeRate = (ulong)(fee / (decimal)size);

Console.WriteLine();

foreach (var (value, count, unique) in inputGroups
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
Console.WriteLine($"Fee paid:\t\t{fee / 100000000m} BTC");
Console.WriteLine($"Size:\t\t\t{size} vbyte");
Console.WriteLine($"Fee rate:\t\t{calculatedFeeRate} sats/vbyte");
Console.WriteLine($"Average anonset:\t{Analyzer.AverageAnonsetGain(inputGroups, outputGroups):0.##}");
Console.WriteLine($"Average input anonset:\t{Analyzer.AverageAnonsetGain(inputGroups):0.##}");
Console.WriteLine($"Average output anonset:\t{Analyzer.AverageAnonsetGain(outputGroups):0.##}");
Console.WriteLine($"Blockspace efficiency:\t{Analyzer.BlockspaceEfficiency(inputGroups, outputGroups, size):0.##}");
Console.WriteLine($"Total leftover:\t\t{mixer.Leftovers.Sum():0}");
Console.WriteLine($"Median leftover:\t{mixer.Leftovers.Median():0}");
Console.WriteLine($"Largest leftover:\t{mixer.Leftovers.Max():0}");
