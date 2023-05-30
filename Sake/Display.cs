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
	    Console.WriteLine($"Number of users:\t{results.Average(r => r.UserCount):0.##}");
	    Console.WriteLine($"Number of inputs:\t{results.Average(r => r.InputCount):0.##}");
	    Console.WriteLine($"Number of outputs:\t{results.Average(r => r.OutputCount):0.##}");
	    Console.WriteLine($"Number of changes:\t{results.Average(r => r.ChangeCount):0.##}");
	    Console.WriteLine($"Total in:\t\t{(decimal)results.Average(r => (double)r.InputAmount) / 100000000m} BTC");
	    Console.WriteLine($"Fee paid for inputs:\t{(decimal)results.Average(r => (double)r.FeeInputs) / 100000000m} BTC");
	    Console.WriteLine($"Fee paid for outputs:\t{(decimal)results.Average(r => (double)r.FeeOutputs) / 100000000m} BTC");
	    Console.WriteLine($"Total fee:\t\t{(decimal)results.Average(r => (double)r.TotalFee) / 100000000m} BTC");
	    Console.WriteLine($"Size:\t\t\t{results.Average(r => r.Size):0.##} vbyte");
	    Console.WriteLine($"Fee rate:\t\t{results.Average(r => r.CalculatedFeeRate):0.##} sats/vbyte");
	    Console.WriteLine($"Anonset:\t\t{results.Average(r => r.AverageAnonset):0.##}");
	    Console.WriteLine($"Input anonset:\t\t{results.Average(r => r.AverageInputAnonset):0.##}");
	    Console.WriteLine($"Output anonset:\t\t{results.Average(r => r.AverageOutputAnonset):0.##}");
	    Console.WriteLine($"Blockspace efficiency:\t{results.Average(r => (double)r.BlockSpaceEfficiency):0.##}");
	    Console.WriteLine($"Total leftover:\t\t{results.Average(r => r.TotalLeftover):0.##}");
	    Console.WriteLine($"Median leftover:\t{results.Where(r => r.MedianLeftover.HasValue).Average(r => r.MedianLeftover!.Value):0.##}");
	    Console.WriteLine($"Largest leftover:\t{results.Average(r => r.LargestLeftover):0.##}");
	    Console.WriteLine($"Taproot/bech32 ratio:\t\t{results.Average(r => r.TaprootCount):0.##}/{results.Average(r => r.Bech32Count):0.##}");
	}

}