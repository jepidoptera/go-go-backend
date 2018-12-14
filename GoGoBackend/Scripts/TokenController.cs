using System;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.ABI;
using Nethereum.StandardTokenEIP20;
using Nethereum.Web3.Accounts;
using SecretKeys;

namespace GoToken
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using Nethereum.Contracts;
    using Nethereum.Hex.HexTypes;
    using Nethereum.Web3;

    public static class TokenController
    {
        // get private key
        static string privatekey;

        //The contract address.
        static string tokenAddress = "";

        //The ABI for the contract.
        static string abi;

        //The URL endpoint for the blockchain network.
        static string url;

        static StandardTokenService goToken;

        // string byteCode = "'0x60806040523480156200001157600080fd5b5033600360006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff160217905550600360009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16600073ffffffffffffffffffffffffffffffffffffffff167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e060405160405180910390a3620000f633601260ff16600a0a61271002620000fc640100000000026401000000009004565b6200027d565b60008273ffffffffffffffffffffffffffffffffffffffff16141515156200012357600080fd5b62000148816002546200025b6401000000000262001323179091906401000000009004565b600281905550620001af816000808573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546200025b6401000000000262001323179091906401000000009004565b6000808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508173ffffffffffffffffffffffffffffffffffffffff16600073ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef836040518082815260200191505060405180910390a35050565b60008082840190508381101515156200027357600080fd5b8091505092915050565b6115aa806200028d6000396000f3006080604052600436106100f1576000357c0100000000000000000000000000000000000000000000000000000000900463ffffffff16806306fdde03146100f6578063095ea7b31461018657806318160ddd146101eb57806323b872dd146102165780632ff2e9dc1461029b578063313ce567146102c657806339509351146102f75780636f8aba4c1461035c57806370a0823114610389578063715018a6146103e05780638da5cb5b146103f75780638f32d59b1461044e57806395d89b411461047d578063a457c2d71461050d578063a9059cbb14610572578063dd62ed3e146105d7578063f2fde38b1461064e575b600080fd5b34801561010257600080fd5b5061010b610691565b6040518080602001828103825283818151815260200191508051906020019080838360005b8381101561014b578082015181840152602081019050610130565b50505050905090810190601f1680156101785780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561019257600080fd5b506101d1600480360381019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190803590602001909291905050506106ca565b604051808215151515815260200191505060405180910390f35b3480156101f757600080fd5b506102006107f7565b6040518082815260200191505060405180910390f35b34801561022257600080fd5b50610281600480360381019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190803573ffffffffffffffffffffffffffffffffffffffff16906020019092919080359060200190929190505050610801565b604051808215151515815260200191505060405180910390f35b3480156102a757600080fd5b506102b06109b3565b6040518082815260200191505060405180910390f35b3480156102d257600080fd5b506102db6109c2565b604051808260ff1660ff16815260200191505060405180910390f35b34801561030357600080fd5b50610342600480360381019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190803590602001909291905050506109c7565b604051808215151515815260200191505060405180910390f35b34801561036857600080fd5b5061038760048036038101908080359060200190929190505050610bfe565b005b34801561039557600080fd5b506103ca600480360381019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190505050610c1e565b6040518082815260200191505060405180910390f35b3480156103ec57600080fd5b506103f5610c66565b005b34801561040357600080fd5b5061040c610d3a565b604051808273ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200191505060405180910390f35b34801561045a57600080fd5b50610463610d64565b604051808215151515815260200191505060405180910390f35b34801561048957600080fd5b50610492610dbc565b6040518080602001828103825283818151815260200191508051906020019080838360005b838110156104d25780820151818401526020810190506104b7565b50505050905090810190601f1680156104ff5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b34801561051957600080fd5b50610558600480360381019080803573ffffffffffffffffffffffffffffffffffffffff16906020019092919080359060200190929190505050610df5565b604051808215151515815260200191505060405180910390f35b34801561057e57600080fd5b506105bd600480360381019080803573ffffffffffffffffffffffffffffffffffffffff1690602001909291908035906020019092919050505061102c565b604051808215151515815260200191505060405180910390f35b3480156105e357600080fd5b50610638600480360381019080803573ffffffffffffffffffffffffffffffffffffffff169060200190929190803573ffffffffffffffffffffffffffffffffffffffff169060200190929190505050611043565b6040518082815260200191505060405180910390f35b34801561065a57600080fd5b5061068f600480360381019080803573ffffffffffffffffffffffffffffffffffffffff1690602001909291905050506110ca565b005b6040805190810160405280600781526020017f476f546f6b656e0000000000000000000000000000000000000000000000000081525081565b60008073ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff161415151561070757600080fd5b81600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040518082815260200191505060405180910390a36001905092915050565b6000600254905090565b6000600160008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002054821115151561088e57600080fd5b61091d82600160008773ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546110e990919063ffffffff16565b600160008673ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055506109a884848461110a565b600190509392505050565b601260ff16600a0a6127100281565b601281565b60008073ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff1614151515610a0457600080fd5b610a9382600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008673ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000205461132390919063ffffffff16565b600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008773ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546040518082815260200191505060405180910390a36001905092915050565b610c06610d64565b1515610c1157600080fd5b610c1b3382611344565b50565b60008060008373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020549050919050565b610c6e610d64565b1515610c7957600080fd5b600073ffffffffffffffffffffffffffffffffffffffff16600360009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e060405160405180910390a36000600360006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff160217905550565b6000600360009054906101000a900473ffffffffffffffffffffffffffffffffffffffff16905090565b6000600360009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff1614905090565b6040805190810160405280600381526020017f47544b000000000000000000000000000000000000000000000000000000000081525081565b60008073ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff1614151515610e3257600080fd5b610ec182600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008673ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546110e990919063ffffffff16565b600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508273ffffffffffffffffffffffffffffffffffffffff163373ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925600160003373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008773ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546040518082815260200191505060405180910390a36001905092915050565b600061103933848461110a565b6001905092915050565b6000600160008473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002060008373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002054905092915050565b6110d2610d64565b15156110dd57600080fd5b6110e681611482565b50565b6000808383111515156110fb57600080fd5b82840390508091505092915050565b6000808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002054811115151561115757600080fd5b600073ffffffffffffffffffffffffffffffffffffffff168273ffffffffffffffffffffffffffffffffffffffff161415151561119357600080fd5b6111e4816000808673ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020546110e990919063ffffffff16565b6000808573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16815260200190815260200160002081905550611277816000808573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000205461132390919063ffffffff16565b6000808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508173ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef836040518082815260200191505060405180910390a3505050565b600080828401905083811015151561133a57600080fd5b8091505092915050565b60008273ffffffffffffffffffffffffffffffffffffffff161415151561136a57600080fd5b61137f8160025461132390919063ffffffff16565b6002819055506113d6816000808573ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020016000205461132390919063ffffffff16565b6000808473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff168152602001908152602001600020819055508173ffffffffffffffffffffffffffffffffffffffff16600073ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef836040518082815260200191505060405180910390a35050565b600073ffffffffffffffffffffffffffffffffffffffff168173ffffffffffffffffffffffffffffffffffffffff16141515156114be57600080fd5b8073ffffffffffffffffffffffffffffffffffffffff16600360009054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff167f8be0079c531659141344cd1fd0a4f28419497f9722a3daafe3b4186f6b6457e060405160405180910390a380600360006101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff160217905550505600a165627a7a7230582006bc55e7efd4e8de7fe7dda84643a996cf0287bed7f359ce2a4dbe11bf006c2d0029'"

        static Account account;
        static Web3 web3;

        static bool init;


        private static void Initialize()
        {
            // set up magic numbers
            tokenAddress = Secrets.key["address"];
            abi = Secrets.key["abi"];
            url = Secrets.key["infura_url"];
            // account 1
            privatekey = Secrets.key["privatekey"];
            // account 2
            // privatekey = "95d16070fca17cb283db778650817b58b42c3ce8c164cd038037ba77bbad80f7";
            account = new Account(privatekey);
            web3 = new Web3(account, url);
            goToken = new StandardTokenService(web3, tokenAddress);
            init = true;
        }

        public static async Task<string> Send(string sendAddress, int amount)
        {
            if (!init) Initialize();
            // set up secrets if that has not been done
            if (tokenAddress == "") Initialize();
            // send tokens to address
            string result = await goToken.TransferAsync<string>(account.Address, sendAddress, amount.ToString(), new HexBigInteger(60000));
            return result;
        }

        public static async Task<int> GetBalance(string address)
        {
            if (!init) Initialize();
            // get balance of an address
            int balance = await goToken.GetBalanceOfAsync<int>(address);
            return balance;
        }

        static string GetSendAmount()
        {
            if (!init) Initialize();
            // get input
            Console.WriteLine("enter amount to send");
            string returnval = Console.ReadLine(); // + "000000000000000000";
            // convert to hex
            returnval = new HexBigInteger(Convert.ToInt64(returnval)).HexValue.Substring(2);
            return returnval;
        }

        public static void ArbitraryFunction(string command = "")
        {
            if (!init) Initialize();
            privatekey = Secrets.key["privatekey"];
            // account 2
            // privatekey = "95d16070fca17cb283db778650817b58b42c3ce8c164cd038037ba77bbad80f7";
            tokenAddress = Secrets.key["address"];
            abi = Secrets.key["abi"];
            url = Secrets.key["infura_url"];
            account = new Account(privatekey);
            web3 = new Web3(account, url);
            Contract goTokenContract = web3.Eth.GetContract(abi, tokenAddress);

            // parse function and parameters
            int paramStart = command.IndexOf('(');
            string function = command.Substring(0, command.IndexOf('('));
            string[] parameters = command.Substring(paramStart).Trim(new char[2] {'(', ')'}).Split(',');
            // convert to appropriate data types
            for (int i = 0; i < parameters.Length; i++)
            {
                if ((string)parameters[i] != "")
                {
                    parameters[i] = parameters[i].Trim(new char[2] { '"', ' ' });
                }
                else
                {
                    break;
                }
            }
            try
            {
                if (parameters.Length == 1 && (string)parameters[0] == "") parameters = null;
                Task<BigInteger> ff = 
                    goTokenContract.GetFunction(function).CallAsync<BigInteger>(account.Address,
                    gas: new HexBigInteger(60000), value: new HexBigInteger(0), functionInput: parameters);
                Console.WriteLine(ff.Result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
