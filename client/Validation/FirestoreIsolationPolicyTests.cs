using System;
using System.Collections.Generic;
using Domino.Infrastructure;

// Pure editor-host doubles. No Unity runtime, Firebase SDK or network is loaded.
namespace UnityEngine { public static class Application { public static bool isBatchMode; } }
namespace UnityEditor { public static class SessionState {
    static readonly Dictionary<string,bool> flags=new Dictionary<string,bool>();
    public static bool GetBool(string key,bool fallback)=>flags.TryGetValue(key,out var value)?value:fallback;
    public static void SetBool(string key,bool value)=>flags[key]=value;
    public static void EraseBool(string key)=>flags.Remove(key);
} }
class FirestoreIsolationPolicyTests
{
    static int checks;
    static void Check(bool value){checks++;if(!value)throw new Exception("F0 policy check failed "+checks);}
    static void Reject(Action action){bool blocked=false;try{action();}catch(InvalidOperationException){blocked=true;}Check(blocked);}
    static void Main()
    {
        var previous=Environment.GetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS");
        try {
            Environment.SetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS",null);
            UnityEngine.Application.isBatchMode=true;
            Check(ValidationNetworkPolicy.Isolated);Reject(ValidationNetworkPolicy.RequireNetwork);Reject(ValidationNetworkPolicy.AuthorizeReal);
            UnityEngine.Application.isBatchMode=false;Check(!ValidationNetworkPolicy.Isolated);
            ValidationNetworkPolicy.BeginIsolated();Check(ValidationNetworkPolicy.Isolated);Reject(ValidationNetworkPolicy.RequireNetwork);
            foreach(var invalid in new[]{"false","TRUE","1",""}){Environment.SetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS",invalid);Reject(ValidationNetworkPolicy.AuthorizeReal);}
            Environment.SetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS","true");
            Check(ValidationNetworkPolicy.Isolated); // External opt-in alone is insufficient.
            ValidationNetworkPolicy.AuthorizeReal();Check(!ValidationNetworkPolicy.Isolated);
            ValidationNetworkPolicy.BeginIsolated();Check(ValidationNetworkPolicy.Isolated); // Visual entry overrides a prior real run.
            ValidationNetworkPolicy.AuthorizeReal();UnityEngine.Application.isBatchMode=true;
            Environment.SetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS",null);Check(ValidationNetworkPolicy.Isolated);
            Check(ValidationNetworkPolicy.NetworkEntrypoints==0);
            ValidationNetworkPolicy.EndValidation();UnityEngine.Application.isBatchMode=false;Check(!ValidationNetworkPolicy.Isolated);
            Console.WriteLine("F0_ISOLATION_POLICY_TESTS="+checks+" PASS; NETWORK_CALLS=0");
        }finally{Environment.SetEnvironmentVariable("DOMINO_REAL_FIRESTORE_TESTS",previous);}
    }
}
