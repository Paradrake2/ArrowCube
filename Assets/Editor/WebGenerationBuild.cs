using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Creates a local WebGL verification build in an isolated test project.</summary>
public static class WebGenerationBuild
{
    public static void Build()
    {
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.initialMemorySize=64;
        PlayerSettings.WebGL.threadsSupport=false;
        PlayerSettings.defaultWebScreenWidth=960; PlayerSettings.defaultWebScreenHeight=600;
        var settings=new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("activeInputHandler").intValue=1; settings.ApplyModifiedPropertiesWithoutUndo();
        string output=Path.GetFullPath("TestResults/WebBuild"); Directory.CreateDirectory(output);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] { "Assets/Scenes/Game.unity","Assets/Scenes/TestScene.unity" },
            locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.Development });
        if(result.summary.result!=BuildResult.Succeeded) throw new System.Exception("WebGL verification build failed: "+result.summary.result);
        Debug.Log("WebGL verification build ready: "+output);
    }
}
