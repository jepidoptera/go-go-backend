using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace GoGoBackend.Models
{
	public class OmiseGo
	{
		const string childChainURL = "localhost:9656";
		public void BroadcastTransaction()
		{
			// rawTransactionInput = [blknum1, txindex1, oindex1, blknum2, txindex2, oindex2, cur12, newowner1, amount1, newowner2, amount2]
			// blknum: the Block number of the transaction within the child chain
			// txindex: the transaction index within the block
			// oindex: the transaction output index
			// cur12: the currency of the transaction - Ethereum address(20 bytes)(all zeroes for ETH)
			// newowner: the address of the new owner of the utxo - Ethereum address(20 bytes)
			// amount: the amount belongs to the new owner of the utxo

		}
	}
}
