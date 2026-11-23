using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Genera el APK de RA Party para instalar directamente en el celular.
public static class RaPartyBuild
{
    public const string OutputPath = "Builds/Android/RAParty.apk";

    [MenuItem("RA Party/4. Generar APK de Android")]
    public static void BuildFromMenu()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUtility.DisplayDialog("RA Party", "Se cambiará la plataforma a Android (puede tardar). ¿Continuar?", "Sí", "Cancelar"))
            return;
        var report = BuildApk();
        EditorUtility.DisplayDialog("RA Party", Summary(report), "OK");
        if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(OutputPath);
    }

    // Unity -batchmode -quit -buildTarget Android -projectPath <ruta> -executeMethod RaPartyBuild.BuildFromCommandLine
    public static void BuildFromCommandLine()
    {
        try
        {
            UISpriteGenerator.GenerateAll();
            var problems = RaPartyUIBuilder.Build();
            foreach (var p in problems) Debug.LogError("[RaPartyBuild] " + p);
            if (problems.Count > 0) { EditorApplication.Exit(2); return; }

            var report = BuildApk();
            Debug.Log("[RaPartyBuild] " + Summary(report));
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static BuildReport BuildApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        EditorUserBuildSettings.buildAppBundle = false;             // .apk, no .aab
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };
        return BuildPipeline.BuildPlayer(options);
    }

    static string Summary(BuildReport report)
    {
        var s = report.summary;
        if (s.result != BuildResult.Succeeded)
            return "El build falló (" + s.result + ") con " + s.totalErrors + " errores. Revisa la consola.";
        return "APK generado: " + OutputPath + " (" + (s.totalSize / (1024f * 1024f)).ToString("0.0") +
               " MB, " + s.totalTime.TotalMinutes.ToString("0.0") + " min)";
    }
}
