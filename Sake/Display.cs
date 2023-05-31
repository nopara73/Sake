namespace Sake;

public static class Display
{
	public static void DisplayResults(List<SimulationResult> results)
	{
	    Console.WriteLine();
	    if (results.Count > 1)
	    {
		    Console.WriteLine($"Average results from {results.Count} iterations");
	    }
	    Console.WriteLine($"Number of users:\t{results.Median(r => r.UserCount):0.##}");
	    Console.WriteLine($"Number of inputs:\t{results.Median(r => r.InputCount):0.##}");
	    Console.WriteLine($"Number of outputs:\t{results.Median(r => r.OutputCount):0.##}");
	    Console.WriteLine($"Number of changes:\t{results.Average(r => r.ChangeCount):0.##}");
	    Console.WriteLine($"Total in:\t\t{(decimal)(results.Median(r => (double)r.InputAmount) ?? 0) / 100000000m} BTC");
	    Console.WriteLine($"Fee paid for inputs:\t{(decimal)(results.Median(r => (double)r.FeeInputs) ?? 0) / 100000000m} BTC");
	    Console.WriteLine($"Fee paid for outputs:\t{(decimal)(results.Median(r => (double)r.FeeOutputs) ?? 0) / 100000000m} BTC");
	    Console.WriteLine($"Total fee:\t\t{(decimal)(results.Median(r => (double)r.TotalFee) ?? 0) / 100000000m} BTC");
	    Console.WriteLine($"Size:\t\t\t{results.Median(r => r.Size):0.##} vbyte");
	    Console.WriteLine($"Fee rate:\t\t{results.Median(r => r.CalculatedFeeRate):0.##} sats/vbyte");
	    Console.WriteLine($"Anonset:\t\t{results.Median(r => r.AverageAnonset):0.##}");
	    Console.WriteLine($"Input anonset:\t\t{results.Median(r => r.AverageInputAnonset):0.##}");
	    Console.WriteLine($"Output anonset:\t\t{results.Median(r => r.AverageOutputAnonset):0.##}");
	    Console.WriteLine($"Blockspace efficiency:\t{results.Median(r => (double)r.BlockSpaceEfficiency):0.##}");
	    Console.WriteLine($"Total leftover:\t\t{results.Median(r => r.TotalLeftover):0.##}");
	    Console.WriteLine($"Median leftover:\t{results.Where(r => r.MedianLeftover.HasValue).Median(r => r.MedianLeftover!.Value):0.##}");
	    Console.WriteLine($"Largest leftover:\t{results.Median(r => r.LargestLeftover):0.##}");
	    Console.WriteLine($"Taproot/bech32 ratio:\t{results.Median(r => r.TaprootCount):0.##}/{results.Median(r => r.Bech32Count):0.##}");
	}

}