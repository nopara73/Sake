using NBitcoin;
using NBitcoin.Policy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
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
            ChangeScriptType = GetNextScriptType();
        }

        public ScriptType ChangeScriptType { get; }
        public Money ChangeFee => FeeRate.GetFee(ChangeScriptType.EstimateOutputVsize());

        public Money MinAllowedOutputAmount { get; }
        public Money MaxAllowedOutputAmount { get; }
        private Random Random { get; }

        public FeeRate FeeRate { get; }
        public bool IsTaprootAllowed { get; }
        public int InputSize { get; } = 69;
        public int OutputSize { get; } = 33;
        public List<int> Leftovers { get; } = new();
        public IOrderedEnumerable<Output> Denominations { get; }
        public List<Output> Outputs { get; } = new();
        private IOrderedEnumerable<Output> CreateDenominations()
        {
            ulong maxSatoshis = MaxAllowedOutputAmount;
            ulong minSatoshis = MinAllowedOutputAmount;
            var denominations = new HashSet<Output>();

            Output CreateDenom(double sats)
            {
                var scriptType = GetNextScriptType();
                return Output.FromDenomination(Money.Satoshis((ulong)sats), scriptType, FeeRate);
            }

            // Powers of 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(2, i));

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 3
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(3, i));

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 3 * 2
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(3, i) * 2);

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(10, i));

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 * 2 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(10, i) * 2);

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Powers of 10 * 5 (1-2-5 series)
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = CreateDenom((ulong)Math.Pow(10, i) * 5);

                if (denom.Amount < minSatoshis)
                {
                    continue;
                }

                if (denom.Amount > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            // Order by EffectiveCost. Greedy decomposer in breakdown should take highest cost value first. 
            return denominations.OrderByDescending(x => x.Amount);
        }

        /// <summary>
        /// Run a series of mix with different input group combinations. 
        /// </summary>
        /// <param name="inputs">Input effective values. The fee substracted, this is how the code works in the original repo.</param>
        /// <returns></returns>
        public IEnumerable<IEnumerable<ulong>> CompleteMix(IEnumerable<IEnumerable<Input>> inputs)
        {
            var inputArray = inputs.ToArray();
            var allInputsEffectiveValue = inputArray.SelectMany(x => x).Select(x => x.EffectiveValue).ToArray();

            var filteredDenominations = GetFilteredDenominations(allInputsEffectiveValue);

            var totalInputCount = allInputsEffectiveValue.Length;

            // This calculation is coming from here: https://github.com/zkSNACKs/WalletWasabi/blob/8b3fb65b/WalletWasabi/WabiSabi/Backend/Rounds/RoundParameters.cs#L48
            StandardTransactionPolicy standardTransactionPolicy = new();
            var maxTransactionSize = standardTransactionPolicy.MaxTransactionSize ?? 100_000;
            var initialInputVsizeAllocation = maxTransactionSize - MultipartyTransactionParameters.SharedOverhead;

            // If we are not going up with the number of inputs above ~400, vsize per alice will be 255. 
            var maxVsizeCredentialValue = Math.Min(initialInputVsizeAllocation / totalInputCount, (int)ProtocolConstants.MaxVsizeCredentialValue);

            for (int i = 0; i < inputArray.Length; i++)
            {
                var currentUser = inputArray[i];
                
                // Calculated totalVsize that we can use. https://github.com/zkSNACKs/WalletWasabi/blob/8b3fb65b/WalletWasabi/WabiSabi/Client/AliceClient.cs#L157
                var availableVsize = currentUser.Sum(input => maxVsizeCredentialValue - input.ScriptType.EstimateInputVsize());
                
                var others = new List<Input>();
                for (int j = 0; j < inputArray.Length; j++)
                {
                    if (i != j)
                    {
                        others.AddRange(inputArray[j]);
                    }
                }
                yield return Decompose(currentUser.Select(x => x.EffectiveValue), filteredDenominations, availableVsize).Select(d => (ulong)d.Amount.Satoshi); ;
            }
        }

        /// <param name="myInputsParam">Input effective values. The fee substracted, this is how the code works in the original repo.</param>
        /// <param name="availableVsize">Calculated totalVsize that we can use for the outputs..</param>
        public IEnumerable<Output> Decompose(IEnumerable<Money> myInputsParam, IEnumerable<Output> denoms, int availableVsize)
        {
            var remainingVsize = availableVsize;
            var myInputs = myInputsParam.ToArray();
            var myInputSum = (ulong)myInputs.Sum();
            var remaining = myInputSum;
            var smallestScriptType = Math.Min(ScriptType.P2WPKH.EstimateOutputVsize(), ScriptType.Taproot.EstimateOutputVsize());
            var maxNumberOfOutputsAllowed = Math.Min(availableVsize / smallestScriptType, 8); // The absolute max possible with the smallest script type.

            var setCandidates = new Dictionary<int, (IEnumerable<Output> Decomp, Money Cost)>();

            // Create the most naive decomposition for starter.
            List<Output> naiveSet = new();
            foreach (var denom in denoms.Where(x => x.EffectiveCost <= remaining))
            {
                bool end = false;
                while (denom.EffectiveCost <= remaining)
                {
                    // We can only let this go forward if at least 2 output can be added (denom + potential change)
                    if (remaining < MinAllowedOutputAmount + ChangeFee || remainingVsize < denom.ScriptType.EstimateOutputVsize() + ChangeScriptType.EstimateOutputVsize())
                    {
                        end = true;
                        break;
                    }

                    naiveSet.Add(denom);
                    remaining -= denom.EffectiveCost;
                    remainingVsize -= denom.ScriptType.EstimateOutputVsize();

                    // Can't have more denoms than max - 1, where -1 is to account for possible change.
                    if (naiveSet.Count >= maxNumberOfOutputsAllowed - 1)
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

            var loss = Money.Zero;
            if (remaining >= MinAllowedOutputAmount + ChangeFee)
            {
                var change = Output.FromAmount(remaining, ChangeScriptType, FeeRate);
                naiveSet.Add(change);
            }
            else
            {
                // This goes to miners.
                loss = remaining;
            }

            // This can happen when smallest denom is larger than the input sum.
            if (naiveSet.Count == 0)
            {
                var change = Output.FromAmount(remaining, ChangeScriptType, FeeRate);
                naiveSet.Add(change);
            }

            HashCode hash = new();
            foreach (var item in naiveSet.OrderBy(x => x.Amount))
            {
                hash.Add(item);
            }

            setCandidates.Add(
                hash.ToHashCode(), // Create hash to ensure uniqueness.
                (naiveSet, loss + CalculateCost(naiveSet)));


            // Create many decompositions for optimization.
            var stdDenoms = denoms.Select(x => x.EffectiveCost.Satoshi).Where(x => (ulong)x <= myInputSum).ToArray();
            var tolerance = (long)Math.Max(loss.Satoshi, 0.5 * (ulong)(MinAllowedOutputAmount + ChangeFee).Satoshi); // Taking the changefee here, might be incorrect however it is just a tolerance.

            if (maxNumberOfOutputsAllowed > 1)
            {
                foreach (var (sum, count, decomp) in Decomposer.Decompose(
                    target: (long)myInputSum,
                    tolerance: tolerance,
                    maxCount: maxNumberOfOutputsAllowed,
                    stdDenoms: stdDenoms))
                {
                    var currentSet = Decomposer.ToRealValuesArray(
                                            decomp,
                                            count,
                                            stdDenoms).Select(Money.Satoshis).ToList();

                    // Translate back to denominations.
                    List<Output> finalDenoms = new();
                    foreach (var outputPlusFee in currentSet)
                    {
                        finalDenoms.Add(denoms.First(d => d.EffectiveCost == outputPlusFee));
                    }

                    // The decomposer won't take vsize into account for different script types, checking it back here if too much, disregard the decomposition.
                    var totalVSize = finalDenoms.Sum(d => d.ScriptType.EstimateOutputVsize());
                    if (totalVSize > availableVsize)
                    {
                        continue;
                    }

                    hash = new();
                    foreach (var item in finalDenoms.OrderBy(x => x.Amount))
                    {
                        hash.Add(item);
                    }

                    var deficit = (myInputSum - (ulong)finalDenoms.Sum(d => d.EffectiveCost)) + CalculateCost(finalDenoms);
                    setCandidates.TryAdd(hash.ToHashCode(), (finalDenoms, deficit));
                }
            }

            var denomHashSet = denoms.ToHashSet();

            var preCandidates = setCandidates.Select(x => x.Value).ToList();
            preCandidates.Shuffle();

            var orderedCandidates = preCandidates
                .OrderBy(x => x.Decomp.Sum(y => denomHashSet.Contains(y) ? Money.Zero : y.Amount)) // Prefer lower change.
                .ThenBy(x => x.Cost) // Less cost is better.
                .ThenBy(x => x.Decomp.Any(d => d.ScriptType == ScriptType.Taproot) && x.Decomp.Any(d => d.ScriptType == ScriptType.P2WPKH) ? 0 : 1) // Prefer mixed scripts types.
                .Select(x => x).ToList();

            // We want to introduce randomity between the best selections.
            var bestCandidateCost = orderedCandidates.First().Cost;
            var costTolerance = Money.Coins(bestCandidateCost.ToUnit(MoneyUnit.BTC) * 1.2m);
            var finalCandidates = orderedCandidates.Where(x => x.Cost <= costTolerance).ToArray();

            // We want to make sure our random selection is not between similar decompositions.
            // Different largest elements result in very different decompositions.
            var largestAmount = finalCandidates.Select(x => x.Decomp.First()).ToHashSet().RandomElement(Random);
            var finalCandidate = finalCandidates.Where(x => x.Decomp.First() == largestAmount).RandomElement(Random).Decomp;

            // Sanity check
            var totalOutputAmount = Money.Satoshis(finalCandidate.Sum(x => x.EffectiveCost));
            if (totalOutputAmount > myInputSum)
            {
                throw new InvalidOperationException("The decomposer is creating money. Aborting.");
            }
            if (totalOutputAmount + MinAllowedOutputAmount + ChangeFee < myInputSum)
            {
                throw new InvalidOperationException("The decomposer is losing money. Aborting.");
            }

            var totalOutputVsize = finalCandidate.Sum(d => d.ScriptType.EstimateOutputVsize());
            if (totalOutputVsize > availableVsize)
            {
                throw new InvalidOperationException("The decomposer created more outputs than it can. Aborting.");
            }

            var leftover = myInputSum - totalOutputAmount;
            if (leftover > MinAllowedOutputAmount + FeeRate.GetFee(NBitcoinExtensions.P2trOutputVirtualSize))
            {
                throw new NotSupportedException($"Leftover too large. Aborting to avoid money loss: {leftover}");
            }
            Leftovers.Add((int)leftover);

            Outputs.AddRange(finalCandidate);

            return finalCandidate;
        }

        private IEnumerable<Output> GetFilteredDenominations(IEnumerable<Money> inputs)
        {
            var secondLargestInput = inputs.OrderByDescending(x => x).Skip(1).First();
            IEnumerable<Output> demonsForBreakDown = Denominations
                .Where(x => x.EffectiveCost <= secondLargestInput)
                .OrderByDescending(x => x.Amount)
                .ThenBy(x => x.EffectiveCost); // If the amount is the same, the cheaper to spend should be the first - so greedy will take that.

            Dictionary<Output, uint> denoms = new();
            foreach (var input in inputs)
            {
                foreach (var denom in BreakDown(input, demonsForBreakDown))
                {
                    if (!denoms.TryAdd(denom, 1))
                    {
                        denoms[denom]++;
                    }
                }
            }

            // Filter out and order denominations those have occured in the frequency table at least twice.
            var preFilteredDenoms = denoms
                .Where(x => x.Value > 1)
                .OrderByDescending(x => x.Key.EffectiveCost)
                .Select(x => x.Key)
            .ToArray();

            // Filter out denominations very close to each other.
            // Heavy filtering on the top, little to no filtering on the bottom,
            // because in smaller denom levels larger users are expected to participate,
            // but on larger denom levels there's little chance of finding each other.
            var increment = 0.5 / preFilteredDenoms.Length;
            List<Output> lessDenoms = new();
            var currentLength = preFilteredDenoms.Length;
            foreach (var denom in preFilteredDenoms)
            {
                var filterSeverity = 1 + currentLength * increment;
                if (!lessDenoms.Any() || denom.Amount.Satoshi <= (lessDenoms.Last().Amount.Satoshi / filterSeverity))
                {
                    lessDenoms.Add(denom);
                }
                currentLength--;
            }

            return lessDenoms;
        }

        /// <summary>
        /// Greedily decomposes an amount to the given denominations.
        /// </summary>
        private IEnumerable<Output> BreakDown(Money input, IEnumerable<Output> denominations)
        {
            var remaining = input;

            foreach (var denom in denominations)
            {
                if (denom.Amount < MinAllowedOutputAmount || remaining < MinAllowedOutputAmount + ChangeFee)
                {
                    break;
                }

                while (denom.EffectiveCost <= remaining)
                {
                    yield return denom;
                    remaining -= denom.EffectiveCost;
                }
            }

            if (remaining >= MinAllowedOutputAmount + ChangeFee)
            {
                var changeOutput = Output.FromAmount(remaining, ScriptType.P2WPKH, FeeRate);
                yield return changeOutput;
            }
        }

        public static Money CalculateCost(IEnumerable<Output> outputs)
        {
            // The cost of the outputs. The more the worst.
            var outputCost = outputs.Sum(o => o.Fee);

            // The cost of sending further or remix these coins.
            var inputCost = outputs.Sum(o => o.InputFee);

            return outputCost + inputCost;
        }

        private ScriptType GetNextScriptType()
        {
            return GetNextScriptType(IsTaprootAllowed, Random);
        }
        
        public static ScriptType GetNextScriptType(bool isTaprootAllowed, Random random)
        {
            if (!isTaprootAllowed)
            {
                return ScriptType.P2WPKH;
            }

            return random.NextDouble() < 0.5 ? ScriptType.P2WPKH : ScriptType.Taproot;
        }
    }
}
