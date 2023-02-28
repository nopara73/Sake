﻿using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using static System.Net.WebRequestMethods;

namespace Sake
{
    internal class Mixer
    {

        /// <param name="feeRate">Bitcoin network fee rate the coinjoin is targeting.</param>
        /// <param name="minAllowedOutputAmount">Minimum output amount that's allowed to be registered.</param>
        /// <param name="maxAllowedOutputAmount">Miximum output amount that's allowed to be registered.</param>
        /// <param name="inputSize">Size of an input.</param>
        /// <param name="outputSize">Size of an output.</param>
        public Mixer(FeeRate feeRate, Money minAllowedOutputAmount, Money maxAllowedOutputAmount, bool isTaprootAllowed, Random? random = null)
        {
            FeeRate = feeRate;
            IsTaprootAllowed = isTaprootAllowed;
            MinAllowedOutputAmount = minAllowedOutputAmount;
            MaxAllowedOutputAmount = maxAllowedOutputAmount;
            Random = random ?? Random.Shared;


            // Create many standard denominations.
            Denominations = CreateDenominations();
        }

        public ulong InputFee => FeeRate.GetFee(InputSize);
        public ulong OutputFee => FeeRate.GetFee(OutputSize);

        public ulong MinAllowedOutputAmountPlusFee => MinAllowedOutputAmount + OutputFee;

        public Money MinAllowedOutputAmount { get; }
        public Money MaxAllowedOutputAmount { get; }
        private Random Random { get; }

        public FeeRate FeeRate { get; }
        public bool IsTaprootAllowed { get; }
        public int InputSize { get; } = 69;
        public int OutputSize { get; } = 33;
        public List<int> Leftovers { get; } = new();
        public IOrderedEnumerable<Output> Denominations { get; }

        private IOrderedEnumerable<Output> CreateDenominations()
        {
            ulong maxSatoshis = MaxAllowedOutputAmount;
            ulong minSatoshis = MinAllowedOutputAmount;
            var denominations = new HashSet<Output>();

            Output CreateDenom(double sats)
            {
                return Output.FromDenomination(Money.Satoshis((ulong)sats), ScriptType.P2WPKH, FeeRate);
            }

            // Powers of 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(2, i));

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
                {
                    break;
                }
                denominations.Add(denom);
            }
            // Powers of 3
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(3, i));

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
                {
                    break;
                }
                denominations.Add(denom);
            }
            // Powers of 3 * 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(3, i) * 2);

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
                {
                    break;
                }
                denominations.Add(denom);
            }
            // Powers of 10 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(10, i));

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
                {
                    break;
                }
                denominations.Add(denom);
            }
            // Powers of 10 * 2 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(10, i) * 2);

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
                {
                    break;
                }
                denominations.Add(denom);
            }
            // Powers of 10 * 5 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom(Math.Pow(10, i) * 5);

                if (denom.Amount < MinAllowedOutputAmount)
                {
                    continue;
                }

                if (denom.Amount > MaxAllowedOutputAmount)
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
            var allInputs= inputArray.SelectMany(x => x).ToArray();

            var totalInputCount = allInputs.Length;

            // This calculation is coming from here: https://github.com/zkSNACKs/WalletWasabi/blob/8b3fb65b/WalletWasabi/WabiSabi/Backend/Rounds/RoundParameters.cs#L48
            StandardTransactionPolicy standardTransactionPolicy = new();
            var maxTransactionSize = standardTransactionPolicy.MaxTransactionSize ?? 100_000;
            var initialInputVsizeAllocation = maxTransactionSize - MultipartyTransactionParameters.SharedOverhead;

            // If we are not going up with the number of inputs above ~400, vsize per alice will be 255. 
            var maxVsizeCredentialValue = Math.Min(initialInputVsizeAllocation / totalInputCount, (int)ProtocolConstants.MaxVsizeCredentialValue);

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


                //Decomposer always takes effective values. See CoinJoinClient.cs in the main repo.
                yield return Decompose(currentUser.Select(c => Money.Satoshis(c) - InputFee), others.Select(c => Money.Satoshis(c) - InputFee), maxVsizeCredentialValue);
            }
        }

        /// <param name="myInputEffectiveValues">One specific client effective input values (minus input fee).</param>
        /// <param name="othersInputEffectiveValues">All the others effective input values.</param>
        /// <param name="maxVsizeCredentialValue">Maximum usable Vsize that client can get per alice.</param>
        public IEnumerable<ulong> Decompose(IEnumerable<Money> myInputEffectiveValues, IEnumerable<Money> othersInputEffectiveValues, int maxVsizeCredentialValue)
        {
            var histogram = GetDenominationFrequencies(myInputEffectiveValues.Concat(othersInputEffectiveValues));

            // Filter out and order denominations those have occured in the frequency table at least twice.
            var preFilteredDenoms = histogram
                .Where(x => x.Value > 1)
                .OrderByDescending(x => x.Key.EffectiveCost)
                .Select(x => (ulong)x.Key.EffectiveCost.Satoshi)
            .ToArray();

            // Calculated totalVsize that we can use. https://github.com/zkSNACKs/WalletWasabi/blob/8b3fb65b/WalletWasabi/WabiSabi/Client/AliceClient.cs#L157
            var availableVsize = (int)myInputEffectiveValues.Sum(input => maxVsizeCredentialValue - InputSize);

            // Filter out denominations very close to each other.
            // Heavy filtering on the top, little to no filtering on the bottom,
            // because in smaller denom levels larger users are expected to participate,
            // but on larger denom levels there's little chance of finding each other.
            var increment = 0.5 / preFilteredDenoms.Length;
            List<ulong> denoms = new();
            var currentLength = preFilteredDenoms.Length;
            foreach(var denom in preFilteredDenoms)
            {
                var filterSeverity = 1 + currentLength * increment;
                if (!denoms.Any() || denom <= (denoms.Last() / filterSeverity))
                {
                    denoms.Add(denom);
                }
                currentLength--;
            }

            var myInputs = myInputEffectiveValues.Select(x => x - InputFee).ToArray();
            var myInputSum = myInputs.Sum();
            var remaining = myInputSum;

            var setCandidates = new Dictionary<int, (IEnumerable<ulong> Decomp, ulong Cost)>();

            // How many times can we participate with the same denomination.
            var maxDenomUsage = Random.Next(2, 8);

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
                (naiveSet, loss + (ulong)naiveSet.Count * OutputFee + (ulong)naiveSet.Count * InputFee)); // The cost is the remaining + output cost + input cost.


            // Create many decompositions for optimization.
            var stdDenoms = denoms.Where(x => x <= myInputSum).Select(x => (long)x).ToArray();
            var maxNumberOfOutputsAllowed = (int)Math.Min(availableVsize / InputSize, 8); // The absolute max possible with the smallest script type.
            var tolerance = (long)Math.Max(loss, 0.5 * MinAllowedOutputAmountPlusFee); // Taking the changefee here, might be incorrect however it is just a tolerance.

            foreach (var (sum, count, decomp) in Decomposer.Decompose(
                target: (long)myInputSum,
                tolerance: tolerance,
                maxCount: Math.Min(maxNumberOfOutputsAllowed, 8),
                stdDenoms: stdDenoms))
            {
                var currentSet = Decomposer.ToRealValuesArray(
                                        decomp,
                                        count,
                                        stdDenoms).Select(Money.Satoshis).ToList();

                hash = new();
                foreach (var item in currentSet.OrderBy(x => x))
                {
                    hash.Add(item);
                }
                setCandidates.TryAdd(hash.ToHashCode(), (currentSet.Select(m => (ulong)m.Satoshi), myInputSum - (ulong)currentSet.Sum() + (ulong)count * OutputFee + (ulong)count * InputFee)); // The cost is the remaining + output cost + input cost.
            }

            var denomHashSet = denoms.ToHashSet();

            var preCandidates = setCandidates.Select(x => x.Value).ToList();
            preCandidates.Shuffle();

            var orderedCandidates = preCandidates
                .OrderBy(x => x.Cost) // Less cost is better.
                .ThenBy(x => x.Decomp.All(x => denomHashSet.Contains(x)) ? 0 : 1) // Prefer no change.
                .Select(x => x).ToList();

            // We want to introduce randomity between the best selections.
            var bestCandidateCost = orderedCandidates.First().Cost;
            var finalCandidates = orderedCandidates.Where(x => x.Cost <= bestCandidateCost * 1.2).ToArray();
            
            // We want to make sure our random selection is not between similar decompositions.
            // Different largest elements result in very different decompositions.
            var largestAmount = finalCandidates.Select(x => x.Decomp.First()).ToHashSet().RandomElement(Random);
            var finalCandidate = finalCandidates.Where(x => x.Decomp.First() == largestAmount).RandomElement(Random).Decomp;

            // Sanity check
            var leftover = myInputSum - finalCandidate.Sum();
            if (leftover > MinAllowedOutputAmountPlusFee)
            {
                throw new NotSupportedException($"Leftover too large. Aborting to avoid money loss: {leftover}");
            }
            Leftovers.Add((int)leftover);

            return finalCandidate.Select(x => x - OutputFee);
        }


        private Dictionary<Output, uint> GetDenominationFrequencies(IEnumerable<Money> inputEffectiveValues)
        {
            var secondLargestInput = inputEffectiveValues.OrderByDescending(x => x).Skip(1).First();

            // The input has to fee it's own price (input effective value already substracted) and can only afford outputs where it's fee can be paid (effective cost).
            var demonsForBreakDown = Denominations.Where(x => x.EffectiveCost <= secondLargestInput);

            Dictionary<Output, uint> denomFrequencies = new();
            foreach (var input in inputEffectiveValues)
            {
                foreach (var denom in BreakDown(input, demonsForBreakDown))
                {
                    if (!denomFrequencies.TryAdd(denom, 1))
                    {
                        denomFrequencies[denom]++;
                    }
                }
            }

            return denomFrequencies;
        }

        /// <summary>
        /// Greedily decomposes an amount to the given denominations.
        /// </summary>
        private IEnumerable<Output> BreakDown(Money input, IEnumerable<Output> denominations)
        {
            var remaining = input - InputFee;

            List<Output> decomposition = new();
            foreach (var denom in denominations)
            {
                // The denomination is too small or change effective cost cannot be paid.
                if (denom.Amount < MinAllowedOutputAmount || remaining < MinAllowedOutputAmount + OutputFee)
                {
                    break;
                }

                while (denom.EffectiveCost <= remaining)
                {
                    decomposition.Add(denom);
                    remaining -= denom.EffectiveCost;
                }
            }

            if (remaining >= MinAllowedOutputAmount + OutputFee)
            {
                var changeOutput = Output.FromAmount(remaining, ScriptType.P2WPKH, FeeRate);
                decomposition.Add(changeOutput);
            }

            return decomposition;
        }
    }
}
