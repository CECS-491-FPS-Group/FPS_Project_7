using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

// Inspect compiled player assemblies; does not execute gameplay.
internal static class AimAssemblyChecks
{
    private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots) { yield return type; foreach (var child in Types(type.NestedTypes)) yield return child; }
    }
    public static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Development DLL, Release DLL required");
        string[] controls = { "DevelopmentAimQuality", "DevelopmentAimFixture", "DevelopmentStopAimFixture", "RunFixture" };
        using (var dev = AssemblyDefinition.ReadAssembly(args[0]))
        {
            var observer = dev.MainModule.Types.Single(t => t.Name == "OccludedAimObserver");
            foreach (string control in controls)
                if (!observer.Methods.Any(m => m.Name == control)) throw new Exception("Development control missing: " + control);
        }
        using (var release = AssemblyDefinition.ReadAssembly(args[1]))
        {
            var types = Types(release.MainModule.Types).ToArray();
            foreach (var type in types)
            {
                if (type.Name.Contains("RunFixture")) throw new Exception("Fixture state machine in Release");
                foreach (var method in type.Methods)
                    if (controls.Contains(method.Name)) throw new Exception("Development control in Release");
            }
            var observer = types.Single(t => t.Name == "OccludedAimObserver");
            if (observer.Fields.Any(f => f.Name.IndexOf("fixture", StringComparison.OrdinalIgnoreCase) >= 0)) throw new Exception("Fixture state in Release");
            if (observer.BaseType.Name != "MonoBehaviour") throw new Exception("Unexpected network layout dependency");
            var shooter = types.Single(t => t.Name == "HitscanShooter");
            string[] parameters = { "Vector3", "UInt32", "Channel", "NetworkConnection" };
            if (!shooter.Methods.Single(m => m.Name == "ReportAim").Parameters.Select(p => p.ParameterType.Name).SequenceEqual(parameters))
                throw new Exception("Unexpected telemetry inputs");
            foreach (var type in types.Where(t => t.Name.StartsWith("OccludedAim") || t.Name.StartsWith("AimIncident") || t.Name == "AimOcclusionGeometry"))
            {
            if (type.Name.StartsWith("AimIncident") && (type.BaseType?.Name == "NetworkBehaviour" || type.Methods.Any(m =>
                m.CustomAttributes.Any(a => a.AttributeType.Name == "ServerRpcAttribute" || a.AttributeType.Name == "ObserversRpcAttribute" || a.AttributeType.Name == "TargetRpcAttribute"))))
                throw new Exception("Incident review introduced network surface: " + type.FullName);
            foreach (var method in type.Methods.Where(m => m.HasBody))
            foreach (var instruction in method.Body.Instructions)
            {
                var call = instruction.Operand as MethodReference;
                if (call != null && (call.DeclaringType.Name == "FileAntiCheat" || call.Name == "Disconnect" || call.Name == "TakeDamage"))
                    throw new Exception("Observation reached punitive/gameplay path: " + call.FullName);
            }
            }
        }
        Console.WriteLine("PASS aim metadata: dev fixture present, Release controls/state absent; feed retained; no observation policy/disconnect/damage calls.");
    }
}
