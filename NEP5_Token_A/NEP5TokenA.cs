using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace NEP5TokenA
{
    public class NEP5TokenA : SmartContract
    {
        //Token settings
        public static string Name() => "NEP5TokenA";
        public static string Symbol() => "NTA";
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; 

        //ICO Settings
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };

        private static byte[] owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();


        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> TransferredEvent;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> RefundEvent;


        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(owner);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "listRefund") return ListRefund();
                if (operation == "mintTokens") return MintTokens();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "currentRate") return 100;
                if (operation == "roundTotal")
                {
                    if (args.Length != 1) return false;
                    return 10000000;
                }
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "decimals") return Decimals();
            }
            return false;
        }
        
        public static bool Deploy()
        {
            return true;
        }
        

        public static bool MintTokens()
        {
            byte[] sender = GetSender();
            // contribute asset is not neo
            if (sender.Length == 0)
            {
                return false;
            }
            
            ulong contribute_value = GetContributeValue();            
            ulong swap_rate = 100;

            // you can get current swap token amount
            ulong token = contribute_value * swap_rate;
            if (token <= 0)
            {
                return false;
            }

            // crowdfunding success
            BigInteger balance = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, token + balance);
            TransferredEvent(null, sender, token);
            return true;
        }
        

        public static void Refund(byte[] sender, BigInteger value) 
        {            
            byte[] refund = Storage.Get(Storage.CurrentContext, "refund");
            byte[] sender_value = IntToBytes(value);
            /** Store the value with the = char between the sender and the value, this allows us to split the values
             *  when trying to show the refunds.
             */
            byte[] new_refund = sender.Concat("=".AsByteArray()).Concat(sender_value);            
            if (refund.Length != 0)
            {               
                /** Split each entry with a "@" symbol. This will allow us to identify each different refund entry. */
                new_refund = refund.Concat("@".AsByteArray()).Concat(new_refund);
            }
            Storage.Put(Storage.CurrentContext, "refund", new_refund);                                               
            RefundEvent(sender, value);
        }
        
        // get the total token supply
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // function that is always called when someone wants to transfer tokens.
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;            
            if (!Runtime.CheckWitness(from)) return false;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return false;
            if (from_value == value)
                Storage.Delete(Storage.CurrentContext, from);
            else
                Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            TransferredEvent(from, to, value);
            return true;
        }

        // get the account balance of another account with address
        public static BigInteger BalanceOf(byte[] address)
        {            
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }
                
        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] reference = tx.GetReferences();
            foreach (TransactionOutput output in reference)
            {
                if (output.AssetId == neo_asset_id) return output.ScriptHash;
            }
            return new byte[0];
        }

        // get smart contract script hash
        private static byte[] GetReceiver()
        {
            return ExecutionEngine.ExecutingScriptHash;
        }

        // get the value of neo that is being passed to this contract to mint tokens
        private static ulong GetContributeValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            // get the total amount of Neo
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == GetReceiver() && output.AssetId == neo_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }
        
        private static byte[] ListRefund()
        {
            return Storage.Get(Storage.CurrentContext, "refund");
        }
                
        private static BigInteger BytesToInt(byte[] array)
        {
            var buffer = new BigInteger(array);
            return buffer;
        }

        private static byte[] IntToBytes(BigInteger value)
        {
            byte[] buffer = value.ToByteArray();
            return buffer;
        }
    }
}
