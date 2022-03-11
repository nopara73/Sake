namespace Sake;

public static class Decomposer
{
	public static long[] StdDenoms;

	public static IEnumerable<(long Sum, int Count, ulong Decomposition)> Decompose(long target, long tolerance, int maxCount)
	{
		var denoms = StdDenoms.SkipWhile(x => x > target).ToArray();

		return denoms.SelectMany((_, i) => InternalCombinations(target, tolerance: tolerance / 10, maxCount, denoms)).Take(10000).ToList();
	}

	private static IEnumerable<(long Sum, int Count, ulong Decomposition)> InternalCombinations(long target, long tolerance, int maxLength, long[] denoms)
	{
		IEnumerable<(long Sum, int Count, ulong Decomposition)> Combinations(
			int currentDenominationIdx,
			ulong accumulator,
			long sum,
			int k)
		{
			accumulator = (accumulator << 8) | ((ulong)currentDenominationIdx & 0xff);
			var currentDenomination = denoms[currentDenominationIdx];
			sum += currentDenomination;
			var remaining = target - sum;
			if (k == 0 || remaining < tolerance)
				return new[] { (sum, maxLength - k, accumulator) };

			currentDenominationIdx = Search(remaining, denoms, currentDenominationIdx);

			return Enumerable.Range(0, denoms.Length - currentDenominationIdx)
				.TakeWhile(i => k * denoms[currentDenominationIdx + i] >= remaining - tolerance)
				.SelectMany((_, i) =>
					Combinations(currentDenominationIdx + i, accumulator, sum, k - 1)
					.TakeUntil(x => x.Sum == target));
		}

		return denoms.SelectMany((_, i) => Combinations(i, 0ul, 0, maxLength - 1)).Take(50).ToList();
	}

	private static int Search(long value, long[] denoms, int offset)
	{
		var startingIndex = Array.BinarySearch(denoms, offset, denoms.Length - offset, value, ReverseComparer.Default);
		return startingIndex < 0 ? ~startingIndex : startingIndex;
	}

	public static IEnumerable<long> ToRealValuesArray(ulong decomposition, int count, long[] denoms)
	{
		var list = new long[count];
		for (var i = 0; i < count; i++)
		{
			var index = (decomposition >> (i * 8)) & 0xff;
			list[count - i - 1] = denoms[index];
		}
		return list;
	}
}

public static class LinqEx
{
	public static IEnumerable<T> TakeUntil<T>(this IEnumerable<T> list, Func<T, bool> predicate)
	{
		foreach (T el in list)
		{
			yield return el;
			if (predicate(el))
				yield break;
		}
	}
}

public class ReverseComparer : IComparer<long>
{
	public static readonly ReverseComparer Default = new();
	public int Compare(long x, long y)
	{
		// Compare y and x in reverse order.
		return y.CompareTo(x);
	}
}