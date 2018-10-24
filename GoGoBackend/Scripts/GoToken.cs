using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.RPC;
using Nethereum.Geth.RPC;
using Nethereum.Geth;
using Nethereum.Web3.Accounts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StringManipulation;

using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer.Crypto;

namespace GoGoBackend.GoToken
{
	public class Token
	{
		static readonly string byteCode = "60c0604052600660808190527f476f436f696e000000000000000000000000000000000000000000000000000060a090815261003e916004919061011a565b506040805180820190915260038082527f474343000000000000000000000000000000000000000000000000000000000060209092019182526100839160059161011a565b506006805460ff19166012179055620f42406007553480156100a457600080fd5b5060038054600160a060020a031916331790819055604051600160a060020a0391909116906000907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e0908290a360065460075460ff909116600a0a026002819055336000908152602081905260409020556101b5565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061015b57805160ff1916838001178555610188565b82800160010185558215610188579182015b8281111561018857825182559160200191906001019061016d565b50610194929150610198565b5090565b6101b291905b80821115610194576000815560010161019e565b90565b610a75806101c46000396000f3006080604052600436106100f05763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146101bd578063095ea7b31461024757806318160ddd1461027f57806323b872dd146102a65780632ff2e9dc146102d0578063313ce567146102e5578063395093511461031057806370a0823114610334578063715018a61461035557806383197ef01461036a5780638da5cb5b1461037f5780638f32d59b146103b057806395d89b41146103c5578063a457c2d7146103da578063a9059cbb146103fe578063dd62ed3e14610422578063f2fde38b14610449575b600034111561015c57604080516020808252808201527f5468616e6b7320666f7220796f757220636f6e747269627574696f6e21203a298183015290517f214701420a87ef056ec61b7e8f3d015eddf859287bb9d149b6fccd3f4296572a9181900360600190a16101bb565b604080516020808252808201527f4572726f72203430343a2046756e6374696f6e206e6f7420666f756e64203a508183015290517fc0d7261505a4cc459dc888a8c4ed401eb2afd6b06d0019132f0f42aa07729e6b9181900360600190a15b005b3480156101c957600080fd5b506101d261046a565b6040805160208082528351818301528351919283929083019185019080838360005b8381101561020c5781810151838201526020016101f4565b50505050905090810190601f1680156102395780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561025357600080fd5b5061026b600160a060020a03600435166024356104f8565b604080519115158252519081900360200190f35b34801561028b57600080fd5b50610294610576565b60408051918252519081900360200190f35b3480156102b257600080fd5b5061026b600160a060020a036004358116906024351660443561057c565b3480156102dc57600080fd5b50610294610619565b3480156102f157600080fd5b506102fa61061f565b6040805160ff9092168252519081900360200190f35b34801561031c57600080fd5b5061026b600160a060020a0360043516602435610628565b34801561034057600080fd5b50610294600160a060020a03600435166106d8565b34801561036157600080fd5b506101bb6106f3565b34801561037657600080fd5b506101bb61075d565b34801561038b57600080fd5b50610394610783565b60408051600160a060020a039092168252519081900360200190f35b3480156103bc57600080fd5b5061026b610792565b3480156103d157600080fd5b506101d26107a3565b3480156103e657600080fd5b5061026b600160a060020a03600435166024356107fe565b34801561040a57600080fd5b5061026b600160a060020a0360043516602435610849565b34801561042e57600080fd5b50610294600160a060020a036004358116906024351661085f565b34801561045557600080fd5b506101bb600160a060020a036004351661088a565b6004805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156104f05780601f106104c5576101008083540402835291602001916104f0565b820191906000526020600020905b8154815290600101906020018083116104d357829003601f168201915b505050505081565b6000600160a060020a038316151561050f57600080fd5b336000818152600160209081526040808320600160a060020a03881680855290835292819020869055805186815290519293927f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925929181900390910190a350600192915050565b60025490565b600160a060020a03831660009081526001602090815260408083203384529091528120548211156105ac57600080fd5b600160a060020a03841660009081526001602090815260408083203384529091529020546105e0908363ffffffff6108a916565b600160a060020a038516600090815260016020908152604080832033845290915290205561060f8484846108c0565b5060019392505050565b60075481565b60065460ff1681565b6000600160a060020a038316151561063f57600080fd5b336000908152600160209081526040808320600160a060020a0387168452909152902054610673908363ffffffff6109b216565b336000818152600160209081526040808320600160a060020a0389168085529083529281902085905580519485525191937f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925929081900390910190a350600192915050565b600160a060020a031660009081526020819052604090205490565b6106fb610792565b151561070657600080fd5b600354604051600091600160a060020a0316907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e0908390a36003805473ffffffffffffffffffffffffffffffffffffffff19169055565b610765610792565b151561077057600080fd5b610778610783565b600160a060020a0316ff5b600354600160a060020a031690565b600354600160a060020a0316331490565b6005805460408051602060026001851615610100026000190190941693909304601f810184900484028201840190925281815292918301828280156104f05780601f106104c5576101008083540402835291602001916104f0565b6000600160a060020a038316151561081557600080fd5b336000908152600160209081526040808320600160a060020a0387168452909152902054610673908363ffffffff6108a916565b60006108563384846108c0565b50600192915050565b600160a060020a03918216600090815260016020908152604080832093909416825291909152205490565b610892610792565b151561089d57600080fd5b6108a6816109cb565b50565b600080838311156108b957600080fd5b5050900390565b600160a060020a0383166000908152602081905260409020548111156108e557600080fd5b600160a060020a03821615156108fa57600080fd5b600160a060020a038316600090815260208190526040902054610923908263ffffffff6108a916565b600160a060020a038085166000908152602081905260408082209390935590841681522054610958908263ffffffff6109b216565b600160a060020a038084166000818152602081815260409182902094909455805185815290519193928716927fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef92918290030190a3505050565b6000828201838110156109c457600080fd5b9392505050565b600160a060020a03811615156109e057600080fd5b600354604051600160a060020a038084169216907f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e090600090a36003805473ffffffffffffffffffffffffffffffffffffffff1916600160a060020a03929092169190911790555600a165627a7a723058204d883891ada13d2d0a0c7b80bbb6d62571d90083aecff2baac6a206e1a63c4140029";
		static readonly string sendAddress = "0x12890d2cce102216644c59daE5baed380d84830c";
		static readonly string password = "password";
		static readonly string privateKey = "";
		public static string url = "https://ropsten.infura.io/v3/4aac5b62b9c94919973195e2bc7f5849";
		static Web3 web3 = new Web3(url: url);
		// static Web3Geth web3 = new Web3Geth(url: url);
		// OR

		public static void BroadcastTransaction()
		{
			// rawTransactionInput = [blknum1, txindex1, oindex1, blknum2, txindex2, oindex2, cur12, newowner1, amount1, newowner2, amount2]
			// blknum: the Block number of the transaction within the child chain
			// txindex: the transaction index within the block
			// oindex: the transaction output index
			// cur12: the currency of the transaction - Ethereum address(20 bytes)(all zeroes for ETH)
			// newowner: the address of the new owner of the utxo - Ethereum address(20 bytes)
			// amount: the amount belongs to the new owner of the utxo

		}

		public static async Task<string> GenerateKey()
		{
			var ecKey = Nethereum.Signer.EthECKey.GenerateKey();
			var privateKey = ecKey.GetPrivateKeyAsBytes().ToHexString();
			var account = new Account(privateKey);
			web3 = new Web3(account, url: url);
			return account.PrivateKey;
		}

		public static async Task<string> DeployContractAsync(out string privateKey)
		{
			string abi = "[{\"constant\":true,\"inputs\":[],\"name\":\"name\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"spender\",\"type\":\"address\"},{\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"approve\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"totalSupply\",\"outputs\":[{\"name\":\"\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"from\",\"type\":\"address\"},{\"name\":\"to\",\"type\":\"address\"},{\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"transferFrom\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"INITIAL_SUPPLY\",\"outputs\":[{\"name\":\"\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"decimals\",\"outputs\":[{\"name\":\"\",\"type\":\"uint8\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"spender\",\"type\":\"address\"},{\"name\":\"addedValue\",\"type\":\"uint256\"}],\"name\":\"increaseAllowance\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"owner\",\"type\":\"address\"}],\"name\":\"balanceOf\",\"outputs\":[{\"name\":\"\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[],\"name\":\"renounceOwnership\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[],\"name\":\"destroy\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"owner\",\"outputs\":[{\"name\":\"\",\"type\":\"address\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"isOwner\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"symbol\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"spender\",\"type\":\"address\"},{\"name\":\"subtractedValue\",\"type\":\"uint256\"}],\"name\":\"decreaseAllowance\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"to\",\"type\":\"address\"},{\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"transfer\",\"outputs\":[{\"name\":\"\",\"type\":\"bool\"}],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[{\"name\":\"owner\",\"type\":\"address\"},{\"name\":\"spender\",\"type\":\"address\"}],\"name\":\"allowance\",\"outputs\":[{\"name\":\"\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"constant\":false,\"inputs\":[{\"name\":\"newOwner\",\"type\":\"address\"}],\"name\":\"transferOwnership\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"constructor\"},{\"payable\":true,\"stateMutability\":\"payable\",\"type\":\"fallback\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"\",\"type\":\"string\"}],\"name\":\"Yes\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"\",\"type\":\"string\"}],\"name\":\"No\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"previousOwner\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"newOwner\",\"type\":\"address\"}],\"name\":\"OwnershipTransferred\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"from\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"to\",\"type\":\"address\"},{\"indexed\":false,\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"Transfer\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"name\":\"owner\",\"type\":\"address\"},{\"indexed\":true,\"name\":\"spender\",\"type\":\"address\"},{\"indexed\":false,\"name\":\"value\",\"type\":\"uint256\"}],\"name\":\"Approval\",\"type\":\"event\"}]";

			// deploy that contract. fingers crossed
			string transactionHash;
			var account = new Account(privateKey);
			try
			{
				var unlockAccountResult = await web3.Personal.UnlockAccount.SendRequestAsync( 120);
				Assert.IsTrue(unlockAccountResult);

				transactionHash = await web3.Eth.DeployContract.SendRequestAsync(byteCode, sendAddress, 
					gas: new HexBigInteger(1000000), gasPrice: new HexBigInteger(20), value: new HexBigInteger(0));
			}
			catch (System.Exception e)
			{
				return e.Message;
			}

			//var mineresult = await web3.Miner.Start.SendRequestAsync(6);
			//Assert.IsTrue(mineresult);

			var receipt = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
			while (receipt == null)
			{
				Thread.Sleep(5);
				receipt = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
			}

			// get the address of the contract... better save this somehow
			string contractAddress = receipt.Result.ContractAddress;
			return contractAddress;
			// Environment.SetEnvironmentVariable("token contract address", contractAddress);
		}
	}
}
