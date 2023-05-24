using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using WalletWasabi.Extensions;

namespace Sake;

public class Input : IEqualityComparer<Input>
{
	public Input(Money amount, ScriptType scriptType, FeeRate feeRate)
	{
		ScriptType = scriptType;
		Fee = feeRate.GetFee(scriptType.EstimateInputVsize());
		Amount = amount;
	}
	
	public Money Amount { get; }
	public ScriptType ScriptType { get; }
	public Money EffectiveValue => Amount - Fee;
	public Money Fee { get; }

	public bool Equals(Input? x, Input? y)
	{
		if (x is null || y is null)
		{
			if (x is null && y is null)
			{
				return true;
			}
			return false;
		}

		if (ReferenceEquals(x, y))
		{
			return true;
		}

		if (x.Amount == y.Amount && x.ScriptType == y.ScriptType && x.Fee == y.Fee)
		{
			return true;
		}

		return false;
	}

	public int GetHashCode([DisallowNull] Input obj) => HashCode.Combine(Amount, ScriptType, Fee);
}
