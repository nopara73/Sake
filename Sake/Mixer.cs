using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sake
{
    internal class Mixer : IMixer
    {
        public Mixer(uint feeRate = 10, uint inputSize = 69, uint outputSize = 33, uint sanityFeeRate = 2, ulong sanityFee = 1000)
        {
            FeeRate = feeRate;
            InputSize = inputSize;
            OutputSize = outputSize;
            var smallestDust = new[] { sanityFee, feeRate * inputSize, sanityFeeRate * inputSize }.Max();
            Denominations = CreateDenominations(smallestDust);
            DustThreshold = Denominations.Last();
        }

        private static IOrderedEnumerable<ulong> CreateDenominations(ulong smallestInclusive)
        {
            ulong maxSatoshis = 2099999997690000;
            var denominations = new HashSet<ulong>();
            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(2, i);

                if (denom < smallestInclusive)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(3, i);

                if (denom < smallestInclusive)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(3, i) * 2;

                if (denom < smallestInclusive)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i);

                if (denom < smallestInclusive)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i) * 2;

                if (denom < smallestInclusive)
                {
                    continue;
                }

                if (denom > maxSatoshis)
                {
                    break;
                }

                denominations.Add(denom);
            }

            for (int i = 0; i < int.MaxValue; i++)
            {
                var denom = (ulong)Math.Pow(10, i) * 5;

                if (denom < smallestInclusive)
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

        public ulong DustThreshold { get; }
        public ulong InputFee => InputSize * FeeRate;
        public ulong OutputFee => OutputSize * FeeRate;

        public uint FeeRate { get; }
        public uint InputSize { get; }
        public uint OutputSize { get; }
        public IOrderedEnumerable<ulong> Denominations { get; }
        public Dictionary<ulong, uint> DenominationProbabilities { get; } = new Dictionary<ulong, uint>();

        public IEnumerable<IEnumerable<ulong>> CompleteMix(IEnumerable<IEnumerable<ulong>> inputs)
        {
            var inputArray = inputs.ToArray();

            SetProbabilities(inputArray.SelectMany(x => x));

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
                yield return Mix(currentUser, others).Select(x => x - OutputFee);
            }
        }

        private void SetProbabilities(IEnumerable<ulong> inputs)
        {
            var secondLargestInput = inputs.OrderByDescending(x => x).Skip(1).First();
            foreach (var input in inputs)
            {
                foreach (var denom in BreakDown(input, secondLargestInput))
                {
                    if (!DenominationProbabilities.TryAdd(denom, 1))
                    {
                        DenominationProbabilities[denom]++;
                    }
                }
            }
        }

        private IEnumerable<ulong> BreakDown(ulong input, ulong secondLargestInput)
        {
            var inputMinusFee = input - InputFee;
            var secondLargestInputMinusFee = secondLargestInput - InputFee;
            ulong dustThresholdPlusFee = DustThreshold + OutputFee;

            var remaining = inputMinusFee;
            foreach (var denomPlusFee in Denominations.Select(x => x + OutputFee))
            {
                if (denomPlusFee > secondLargestInputMinusFee)
                {
                    continue;
                }

                if (denomPlusFee < dustThresholdPlusFee || remaining < dustThresholdPlusFee)
                {
                    break;
                }

                while (denomPlusFee <= remaining)
                {
                    yield return denomPlusFee;
                    remaining -= denomPlusFee;
                }
            }

            if (remaining >= dustThresholdPlusFee)
            {
                yield return remaining;
            }
        }

        public IEnumerable<ulong> Mix(IEnumerable<ulong> myInputsParam, IEnumerable<ulong> othersInputsParam)
        {
            var setCandidates = new Dictionary<int, IEnumerable<ulong>>();

            var myInputs = myInputsParam.Select(x => x - InputFee).ToArray();

            ulong dustThresholdPlusFee = DustThreshold + OutputFee;

            var denoms = DenominationProbabilities.OrderByDescending(x => x.Key).Where(x => x.Value > 1).Select(x => x.Key).ToList();

            var naiveSet = new List<ulong>();
            var remaining = myInputs.Sum();
            foreach (var denomPlusFee in denoms.Where(x => x <= remaining))
            {
                if (remaining < dustThresholdPlusFee)
                {
                    break;
                }

                while (denomPlusFee <= remaining)
                {
                    naiveSet.Add(denomPlusFee);
                    remaining -= denomPlusFee;
                }
            }

            if (remaining >= dustThresholdPlusFee)
            {
                naiveSet.Add(remaining);
            }

            setCandidates.Add(naiveSet.Count, naiveSet);

            var sw = Stopwatch.StartNew();
            while (true)
            {
                var currSet = new List<ulong>();
                remaining = myInputs.Sum();
                while (true)
                {
                    var denomPlusFees = denoms.Where(x => x <= remaining && x >= (remaining / 3)).ToList();
                    var denomPlusFee = denomPlusFees.RandomElement();
                    if (remaining < dustThresholdPlusFee)
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
                    if (remaining >= dustThresholdPlusFee)
                    {
                        currSet.Add(remaining);
                    }

                    setCandidates.TryAdd(currSet.Count, currSet);
                }

                if (sw.ElapsedMilliseconds > 30)
                {
                    break;
                }
            }

            return setCandidates.RandomElement().Value;
        }
    }
}
