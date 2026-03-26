using Nethereum.Signer;
var key = new EthECKey("62e5b67b3fcfe4e7621d61be4f5a0d1f768e70f693dbee14b3e6b085431f0ae5");
Console.WriteLine("Derived address: " + key.GetPublicAddress());
