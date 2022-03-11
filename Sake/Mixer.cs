using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sake
{
    internal class Mixer
    {
        /// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
        /// <param name="minAllowedOutputAmount">Minimum output amount that's allowed to be registered.</param>
        /// <param name="inputSize">Size of an input.</param>
        /// <param name="outputSize">Size of an output.</param>
        public Mixer(uint feeRate = 10, ulong minAllowedOutputAmount = 5000, uint inputSize = 69, uint outputSize = 33)
        {
            FeeRate = feeRate;
            InputSize = inputSize;
            OutputSize = outputSize;

            MinAllowedOutputAmountPlusFee = minAllowedOutputAmount + OutputFee;

            // Create many standard denominations.
            DenominationsPlusFees = CreateDenominationsPlusFees();
        }

        public ulong InputFee => InputSize * FeeRate;
        public ulong OutputFee => OutputSize * FeeRate;

        public ulong MinAllowedOutputAmountPlusFee { get; }

        public uint FeeRate { get; }
        public uint InputSize { get; }
        public uint OutputSize { get; }
        public List<int> Leftovers { get; } = new();
        public IOrderedEnumerable<ulong> DenominationsPlusFees { get; }

        /// <summary>
        /// Pair of denomination and the number of times we found it in a breakdown.
        /// </summary>
        public Dictionary<ulong, uint> DenominationFrequencies { get; } = new Dictionary<ulong, uint>();

        private IOrderedEnumerable<ulong> CreateDenominationsPlusFees()
        {
            ulong maxSatoshis = 2099999997690000;
            ulong minSatoshis = MinAllowedOutputAmountPlusFee;
            var denominations = new HashSet<ulong>();

            // Powers of 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(2, i) + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 3
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(3, i) + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 3 * 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(3, i) * 2 + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i) + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 * 2 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i) * 2 + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 * 5 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i) * 5 + OutputFee;

                if (denom < minSatoshis)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            return denominations.OrderByDescending(x => x);
        }

        public IEnumerable<IEnumerable<ulong>> CompleteMix(IEnumerable<IEnumerable<ulong>> inputs)
        {
            var inputArray = inputs.ToArray();

            SetDenominationFrequencies(inputArray.SelectMany(x => x));

            for (int i = 0; i < inputArray.Length; i++)
            {
                var currentUser = inputArray[i];
                var others = new List<ulong>();
                for (int j = 0; j < inputArray.Length; j++)
                {
                    if (i != j)
                    {
                        others.AddRange(inputArray[j]);
                    }
                }
                yield return Decompose(currentUser, others);
            }
        }

        public IEnumerable<ulong> Decompose(IEnumerable<ulong> myInputsParam, IEnumerable<ulong> othersInputsParam)
        {
            // Filter out and order denominations those have occured in the frequency table at least twice.
            var denoms = DenominationFrequencies
                .Where(x => x.Value > 1)
                .OrderByDescending(x => x.Key)
                .Select(x => x.Key)
                .ToArray();

            var myInputs = myInputsParam.Select(x => x - InputFee).ToArray();
            var myInputSum = myInputs.Sum();
            var remaining = myInputSum;

            var setCandidates = new Dictionary<int, (IEnumerable<ulong> Decomp, ulong Cost)>();
            var random = new Random();

            // How many times can we participate with the same denomination.
            var maxDenomUsage = random.Next(2, 8);

            // Create the most naive decomposition for starter.
            List<ulong> naiveSet = new();
            bool end = false;
            foreach (var denomPlusFee in denoms.Where(x => x <= remaining))
            {
                var denomUsage = 0;
                while (denomPlusFee <= remaining)
                {
                    if (remaining < MinAllowedOutputAmountPlusFee)
                    {
                        end = true;
                        break;
                    }

                    naiveSet.Add(denomPlusFee);
                    remaining -= denomPlusFee;
                    denomUsage++;

                    // If we reached the limit, the rest will be change.
                    if (denomUsage >= maxDenomUsage)
                    {
                        end = true;
                        break;
                    }
                }

                if (end)
                {
                    break;
                }
            }

            var loss = 0UL;
            if (remaining >= MinAllowedOutputAmountPlusFee)
            {
                naiveSet.Add(remaining);
            }
            else
            {
                // This goes to miners.
                loss = remaining;
            }

            // This can happen when smallest denom is larger than the input sum.
            if (naiveSet.Count == 0)
            {
                naiveSet.Add(remaining);
            }

            HashCode hash = new();
            foreach (var item in naiveSet.OrderBy(x => x))
            {
                hash.Add(item);
            }

            setCandidates.Add(
                hash.ToHashCode(), // Create hash to ensure uniqueness.
                (naiveSet, loss + (ulong)naiveSet.Count * OutputFee)); // The cost is the remaining + output cost.

            // Create many decompositions for optimization.
            Decomposer.StdDenoms = denoms.Where(x => x <= myInputSum).Select(x => (long)x).ToArray();
            foreach (var (sum, count, decomp) in Decomposer.Decompose((long)myInputSum, (long)loss, Math.Max(5, naiveSet.Count)))
            {
                var currentSet = Decomposer.ToRealValuesArray(
                    decomp,
                    count,
                    Decomposer.StdDenoms).Cast<ulong>().ToArray();

                hash = new();
                foreach (var item in currentSet.OrderBy(x => x))
                {
                    hash.Add(item);
                }
                setCandidates.TryAdd(hash.ToHashCode(), (currentSet, myInputSum - (ulong)sum + (ulong)count * OutputFee)); // The cost is the remaining + output cost.
            }

            var denomHashSet = denoms.ToHashSet();

            var finalCandidates = setCandidates.Select(x => x.Value).ToList();
            finalCandidates.Shuffle();

            var orderedCandidates = finalCandidates
                .OrderBy(x => x.Cost) // Less cost is better.
                .ThenBy(x => x.Decomp.All(x => denomHashSet.Contains(x)) ? 0 : 1) // Prefer no change.
                .Select(x => x).ToList();

            var finalCandidate = orderedCandidates.First().Decomp;
            foreach (var candidate in orderedCandidates)
            {
                if (random.NextDouble() < 0.5)
                {
                    finalCandidate = candidate.Decomp;
                    Leftovers.Add((int)(myInputSum - candidate.Decomp.Sum()));
                    break;
                }
            }

            return finalCandidate.Select(x => x - OutputFee);
        }


        private void SetDenominationFrequencies(IEnumerable<ulong> inputs)
        {
            var secondLargestInput = inputs.OrderByDescending(x => x).Skip(1).First();
            IEnumerable<ulong> demonsForBreakDown = DenominationsPlusFees.Where(x => x <= secondLargestInput - InputFee);

            foreach (var input in inputs)
            {
                foreach (var denom in BreakDown(input, demonsForBreakDown))
                {
                    if (!DenominationFrequencies.TryAdd(denom, 1))
                    {
                        DenominationFrequencies[denom]++;
                    }
                }
            }
        }

        /// <summary>
        /// Greedily decomposes an amount to the given denominations.
        /// </summary>
        private IEnumerable<ulong> BreakDown(ulong input, IEnumerable<ulong> denominations)
        {
            var remaining = input - InputFee;

            foreach (var denomPlusFee in denominations)
            {
                if (denomPlusFee < MinAllowedOutputAmountPlusFee || remaining < MinAllowedOutputAmountPlusFee)
                {
                    break;
                }

                while (denomPlusFee <= remaining)
                {
                    yield return denomPlusFee;
                    remaining -= denomPlusFee;
                }
            }

            if (remaining >= MinAllowedOutputAmountPlusFee)
            {
                yield return remaining;
            }
        }
    }
}
