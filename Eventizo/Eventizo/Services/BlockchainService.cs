using Microsoft.Extensions.Configuration;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Eventizo.Services
{
    public class BlockchainService
    {
        private readonly Web3 _web3;
        private readonly string _contractAddress;

        private const string ABI = @"[
            {
                ""inputs"": [
                    { ""internalType"": ""string"", ""name"": ""data"", ""type"": ""string"" }
                ],
                ""name"": ""addBlock"",
                ""outputs"": [],
                ""stateMutability"": ""nonpayable"",
                ""type"": ""function""
            },
            {
                ""inputs"": [
                    { ""internalType"": ""uint256"", ""name"": ""index"", ""type"": ""uint256"" }
                ],
                ""name"": ""getBlock"",
                ""outputs"": [
                    { ""internalType"": ""uint256"", ""name"": """", ""type"": ""uint256"" },
                    { ""internalType"": ""uint256"", ""name"": """", ""type"": ""uint256"" },
                    { ""internalType"": ""string"", ""name"": """", ""type"": ""string"" },
                    { ""internalType"": ""string"", ""name"": """", ""type"": ""string"" },
                    { ""internalType"": ""string"", ""name"": """", ""type"": ""string"" }
                ],
                ""stateMutability"": ""view"",
                ""type"": ""function""
            },
            {
                ""inputs"": [],
                ""name"": ""getChainCount"",
                ""outputs"": [
                    { ""internalType"": ""uint256"", ""name"": """", ""type"": ""uint256"" }
                ],
                ""stateMutability"": ""view"",
                ""type"": ""function""
            }
        ]";

        public BlockchainService(IConfiguration config)
        {
            var rpcUrl = config["Blockchain:RpcUrl"];
            var privateKey = config["Blockchain:PrivateKey"];
            _contractAddress = config["Blockchain:ContractAddress"];

            var chainId = new Nethereum.Hex.HexTypes.HexBigInteger(1337);

            var account = new Account(privateKey, chainId);
            _web3 = new Web3(account, rpcUrl);
        }

        public async Task<string> AddBlockAsync(string data)
        {
            var contract = _web3.Eth.GetContract(ABI, _contractAddress);
            var addBlockFunc = contract.GetFunction("addBlock");

            string txHash = await addBlockFunc.SendTransactionAsync(
                _web3.TransactionManager.Account.Address,
                new Nethereum.Hex.HexTypes.HexBigInteger(300000),
                null,
                data
            );

            return txHash;
        }

        public async Task<int> GetChainCountAsync()
        {
            var contract = _web3.Eth.GetContract(ABI, _contractAddress);
            var func = contract.GetFunction("getChainCount");

            var result = await func.CallAsync<uint>();
            return (int)result;
        }

        // Sửa GetBlockAsync để lấy transaction hashes
        public async Task<BlockDto> GetBlockAsync(int index)
        {
            // Lấy block từ Ethereum node với transactions
            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                .SendRequestAsync(new Nethereum.Hex.HexTypes.HexBigInteger(index));

            var contractFunc = _web3.Eth.GetContract(ABI, _contractAddress)
                .GetFunction("getBlock");

            var result = await contractFunc.CallDeserializingToObjectAsync<BlockDto>(index);

            // Gán transaction hashes
            result.TransactionHashes = block.Transactions.Select(tx => tx.TransactionHash).ToList();

            return result;
        }

        public async Task<Nethereum.RPC.Eth.DTOs.Transaction> GetTransactionByHashAsync(string txHash)
        {
            return await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(txHash);
        }
    }

    public class BlockDto
    {
        public uint Index { get; set; }
        public uint Timestamp { get; set; }
        public string Data { get; set; }
        public string PreviousHash { get; set; }
        public string Hash { get; set; }
        public List<string> TransactionHashes { get; set; }
    }
}
