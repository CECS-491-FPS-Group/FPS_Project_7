using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the actual exported Mono assembly as well as compiler profiles. Does not launch or emulate a player.
internal static class ReleaseSurfaceChecks
{
    private static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> roots)
    { foreach (var type in roots) { yield return type; foreach (var nested in All(type.NestedTypes)) yield return nested; } }
    private static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    private static readonly string[] Controls = { "DevelopmentAimQuality", "DevelopmentAimFixture", "DevelopmentStopAimFixture", "RunFixture",
        "DevelopmentCycleMovementProbe", "DevelopmentResetMovementProbe", "DevelopmentSetMovementProbe", "DevelopmentPlace",
        "DevelopmentSampleQuality", "DevelopmentConfigureNetwork", "DevelopmentLobbyProbe", "DevelopmentConfigurationProbe",
        "DevelopmentControls", "DevelopmentRequestProbe", "DevelopmentGrantBoost", "GrantTestBoost", "TestArmor", "TestInvisibility",
        "TestSpeed", "TestShield", "TestJump", "TestDamage" };
    public static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Development profile DLL and exported/profile Release DLL required");
        using (var dev = AssemblyDefinition.ReadAssembly(args[0]))
        {
            var methods = All(dev.MainModule.Types).SelectMany(t => t.Methods).ToArray();
            foreach (string control in Controls) Require(methods.Any(m => m.Name == control), "Development acceptance control missing: " + control);
        }
        using (var release = AssemblyDefinition.ReadAssembly(args[1]))
        {
            var types = All(release.MainModule.Types).ToArray(); var methods = types.SelectMany(t => t.Methods).ToArray();
            foreach (var type in types)
            {
                Require(!type.Name.Contains("RunFixture"), "Release fixture coroutine: " + type.FullName);
                Require(!type.Fields.Any(f => f.Name.StartsWith("_development") || f.Name.StartsWith("_fixture")), "Release probe state: " + type.FullName);
            }
            foreach (var method in methods)
            {
                Require(!Controls.Contains(method.Name), "Release development launcher: " + method.FullName);
                if (!method.HasBody) continue;
                foreach (var instruction in method.Body.Instructions)
                {
                    string literal = instruction.Operand as string;
                    Require(literal != "FPS_INTEGRITY_TEST_STARTUP" && literal != "FPS_MOVEMENT_TEST_NETWORK" &&
                        !(literal?.StartsWith("Development:") ?? false), "Reachable Release test environment/menu: " + method.FullName);
                    var call = instruction.Operand as MethodReference;
                    Require(call == null || !new[] { "get_f6Key", "get_f7Key", "get_f8Key", "get_f9Key", "get_f10Key" }.Contains(call.Name), "Release F-key handler: " + method.FullName);
                    Require(call == null || !Controls.Contains(call.Name), "Release call into a development control: " + method.FullName);
                }
            }
            foreach (var pair in new[] { "FileAntiCheat.Receive", "FileAntiCheat.BeginCheck", "FileAntiCheat.ApplyPolicy", "HitscanShooter.ReportAim",
                "SpeedHackDetector.LateUpdate", "ClientDisconnectRecovery.ShowNotice", "ClientDisconnectRecovery.Dismiss",
                "LocalPlayerControls.OpenMenu", "LocalReplayCapture.Save", "LocalReplayMenu.Show", "AimIncidentReviewMenu.Show", "AimIncidentArchive.SaveReview" })
            {
                string[] names = pair.Split('.');
                Require(types.Single(t => t.Name == names[0]).Methods.Any(m => m.Name == names[1]), "Intended Release behavior removed: " + pair);
            }
            var policy = types.Single(t => t.Name == "FileAntiCheat");
            Require(types.Single(t => t.Name == "EnforcementMode").Fields.Single(f => f.Name == "ReportOnly").Constant.Equals(0), "Unexpected policy default enum");
            foreach (string field in new[] { "staticMode", "dynamicMode" })
            foreach (var constructor in policy.Methods.Where(m => m.Name == ".ctor" && m.HasBody))
            foreach (var instruction in constructor.Body.Instructions.Where(i => i.OpCode == OpCodes.Stfld && (i.Operand as FieldReference)?.Name == field))
                Require(instruction.Previous.OpCode == OpCodes.Ldc_I4_0, "Committed mode does not default to ReportOnly: " + field);
            // Removing F9 UI is insufficient: the Release server must also refuse a remote recheck.
            var accept = policy.Methods.Single(m => m.Name == "Receive").Body.Instructions.Single(i =>
                (i.Operand as MethodReference)?.DeclaringType.Name == "IntegrityReportLifecycle" && (i.Operand as MethodReference)?.Name == "TryAccept");
            Require(accept.Previous.Previous.OpCode == OpCodes.Ldc_I4_0, "Release server recheck argument must be false");
        }
        Console.WriteLine("PASS Release surface: controls/state/menu/F6-F10 absent, intended player/review paths retained, ReportOnly defaults, remote rechecks disabled; Development tools retained.");
    }
}
