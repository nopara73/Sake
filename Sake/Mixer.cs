using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sake
{
    internal class Mixer : IMixer
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
                yield return Decompose(currentUser, others).Select(x => x - OutputFee);
            }
        }

        public IEnumerable<ulong> Decompose(IEnumerable<ulong> myInputsParam, IEnumerable<ulong> othersInputsParam)
        {
            var setCandidates = new Dictionary<ulong, (IEnumerable<ulong> Decomp, ulong Cost)>();
            var random = new Random();
            var maxDenomUsage = random.Next(2, 8);

            var myInputs = myInputsParam.Select(x => x - InputFee).ToArray();

            var denoms = DenominationFrequencies.OrderByDescending(x => x.Key).Where(x => x.Value > 1).Select(x => x.Key).ToList();

            var naiveSet = new List<ulong>();
            var remaining = myInputs.Sum();
            foreach (var denomPlusFee in denoms.Where(x => x <= remaining))
            {
                if (remaining < MinAllowedOutputAmountPlusFee)
                {
                    break;
                }

                var denomUsage = 0;
                while (denomPlusFee <= remaining)
                {
                    naiveSet.Add(denomPlusFee);
                    remaining -= denomPlusFee;

                    denomUsage++;
                    if (denomUsage >= maxDenomUsage)
                    {
                        break;
                    }
                }

                if (denomUsage >= maxDenomUsage)
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
                loss = remaining;
            }

            // This can happen when smallest denom is larger than the input sum.
            if (naiveSet.Count == 0)
            {
                naiveSet.Add(remaining);
            }

            setCandidates.Add(naiveSet.OrderBy(x => x).Aggregate((x, y) => 31 * x + y), (naiveSet, loss + (ulong)naiveSet.Count * OutputFee));

            var before = DateTimeOffset.UtcNow;
            while (true)
            {
                var currSet = new List<ulong>();
                remaining = myInputs.Sum();
                while (true)
                {
                    var denomPlusFees = denoms.Where(x => x <= remaining && x >= (remaining / 3)).ToList();
                    var denomPlusFee = denomPlusFees.RandomElement();

                    if (remaining < MinAllowedOutputAmountPlusFee)
                    {
                        break;
                    }

                    if (denomPlusFee <= remaining)
                    {
                        currSet.Add(denomPlusFee);
                        remaining -= denomPlusFee;
                    }

                    if (currSet.Count > naiveSet.Count && currSet.Count > 3) break;
                }

                if (currSet.Count <= naiveSet.Count || currSet.Count <= 3)
                {
                    loss = 0;
                    if (remaining >= MinAllowedOutputAmountPlusFee)
                    {
                        currSet.Add(remaining);
                    }
                    else
                    {
                        loss = remaining;
                    }

                    if (currSet.Count == 0)
                    {
                        currSet.Add(remaining);
                    }

                    setCandidates.TryAdd(currSet.OrderBy(x => x).Aggregate((x, y) => 31 * x + y), (currSet, loss + (ulong)currSet.Count * OutputFee));
                }

                if ((DateTimeOffset.UtcNow - before).TotalMilliseconds > 30)
                {
                    break;
                }
            }

            var denomHashSet = denoms.ToHashSet();

            var finalCandidates = setCandidates.Select(x => (x.Value)).ToList();
            finalCandidates.Shuffle();
            var orderedCandidates = finalCandidates
                .OrderBy(x => x.Cost)
                .ThenBy(x => x.Decomp.All(x => denomHashSet.Contains(x)) ? 0 : 1)
                .Select(x => x).ToList();

            foreach (var candidate in orderedCandidates)
            {
                var r = random.Next(0, 10);
                if (r < 5)
                {
                    //Console.WriteLine(candidate.Cost - (ulong)candidate.Decomp.Count() * OutputFee);
                    return candidate.Decomp;
                }
            }

            return orderedCandidates.First().Decomp;
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
