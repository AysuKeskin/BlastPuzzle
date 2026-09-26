using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Explicit build purposes. Simulator selection is temporary and restored afterwards.
public static class MobileBuild
{
    [MenuItem("BlastPuzzle/Build/iOS Device Release")]
    public static void DeviceRelease() => Build(false, false);
    [MenuItem("BlastPuzzle/Build/iOS Simulator Release")]
    public static void SimulatorRelease() => Build(true, false);
    [MenuItem("BlastPuzzle/Build/iOS Device Development")]
    public static void DeviceDevelopment() => Build(false, true);

    private static void Build(bool simulator, bool development)
    {
        var previousSdk = PlayerSettings.iOS.sdkVersion;
        var previousArchitecture = PlayerSettings.iOS.simulatorSdkArchitecture;
        string output = "Builds/iOS-" + (simulator ? "Simulator" : "Device") + (development ? "-Development" : "-Release");
        try
        {
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.iOS,
                options = development ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/M22-build-result.txt", report.summary.result + "\n" + output +
                "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Mobile build failed; see Logs/M22-build-result.txt and Editor log.");
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = previousSdk;
            PlayerSettings.iOS.simulatorSdkArchitecture = previousArchitecture;
        }
    }
}
