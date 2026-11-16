using ArrowCube.Generation;
using ArrowCube.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DifficultyProfileAsset))]
public sealed class DifficultyProfileAssetEditor : Editor
{
    private int preset=1,choice=5;
    public override void OnInspectorGUI()
    {
        var asset=(DifficultyProfileAsset)target;
        EditorGUILayout.LabelField("Measured puzzle difficulty",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        int selected=EditorGUILayout.Popup(new GUIContent("Preset","Selecting a preset explicitly replaces all profile and search values."),choice,new[] { "Easy","Normal","Hard","Expert","Extreme","Custom" });
        if(EditorGUI.EndChangeCheck())
        {
            choice=selected;
            if(selected<5) { Undo.RecordObject(asset,"Apply difficulty preset"); preset=selected; asset.ApplyPreset(selected); EditorUtility.SetDirty(asset); }
        }
        EditorGUI.BeginChangeCheck();
        float master=EditorGUILayout.Slider(new GUIContent("Master difficulty","Changes a coherent set of parameters. Edit individual sliders below to preserve unrelated Custom settings."),(float)(asset.difficultySettings?.TargetScore??30),0,100);
        if(EditorGUI.EndChangeCheck()) { Undo.RecordObject(asset,"Set master difficulty"); asset.ApplyMasterDifficulty(master); choice=5; EditorUtility.SetDirty(asset); }
        if(GUILayout.Button("Reset all settings to "+DifficultyPresets.All[preset].Name))
        { Undo.RecordObject(asset,"Reset difficulty defaults"); asset.ApplyPreset(preset); choice=preset; EditorUtility.SetDirty(asset); }
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject,"m_Script","difficultySettings");
        var settings=serializedObject.FindProperty("difficultySettings");
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Logical targets (soft)",EditorStyles.boldLabel);
        Slider(settings,"TargetScore",0,100,"Target score","Independent target; does not modify size or other Custom settings.");
        Slider(settings,"ScoreTolerance",0,50,"Score tolerance","Return the best valid candidate with an explicit warning when outside this range.");
        Slider(settings,"DesiredSafeFraction",0,1,"Desired safe fraction","Desired fraction of immediately removable paths.");
        Slider(settings,"DesiredDependencyDepth",1,50,"Desired dependency depth","Desired average number of fixed-route blocker layers.");
        Slider(settings,"DependencyStrength",0,1,"Dependency strength","Prefer new paths that block several older routes, including existing blocked routes.");
        Slider(settings,"EdgeResistance",0,1,"Edge-first resistance","Prefer dependencies from edge paths to interior blockers.");
        Slider(settings,"SpatialUnpredictability",0,1,"Spatial unpredictability","Prefer safe/blocked arrangements that cannot be predicted from edge distance.");
        Slider(settings,"CrossFaceFrequency",0,1,"Cross-face frequency","Additional seam-crossing proposal frequency; the mandatory quota is separate.");
        EditorGUILayout.PropertyField(settings.FindPropertyRelative("Placement"),new GUIContent("Placement strategy","Uniform, interior, edge, mixed or dependency-optimized candidate placement."));
        EditorGUI.BeginChangeCheck();
        var quality=settings.FindPropertyRelative("Quality");
        EditorGUILayout.PropertyField(quality,new GUIContent("Search quality","Choosing a quality preset resets candidate, proposal-pool and optimization budgets only."));
        bool qualityChanged=EditorGUI.EndChangeCheck();
        foreach(string name in new[] { "MaxCandidates","MaxConstructionAttempts","MaxRestarts","OptimizationIterations","CandidatePoolSize","AlternativeOrderSamples" })
            EditorGUILayout.PropertyField(settings.FindPropertyRelative(name));
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Independent multi-blocker generation",EditorStyles.boldLabel);
        var multi=settings.FindPropertyRelative("MultiBlocker");
        foreach(string name in new[] { "Enabled","EnableTargetedPlacement","ReplaceExistingPaths","MaxTargetBlockers","TargetedBlockerAttempts" })
            EditorGUILayout.PropertyField(multi.FindPropertyRelative(name));
        Slider(multi,"TargetAverageBlockers",0,8,"Average blockers / blocked path","Soft target. Confirmed direct blockers and independent branches are measured separately.");
        Slider(multi,"MultiBlockerPathFraction",0,1,"Independent multi-blocker fraction","Desired fraction with at least two incomparable mandatory blockers.");
        Slider(multi,"CrossFacePreference",0,1,"Cross-face dependency preference","Actual blocker relationships involving multi-face geometry, not just a count of cross-face paths.");
        Slider(multi,"BranchingPreference",0,1,"Branching preference","Prefer distributed dependencies, avoiding an obvious blocker hub.");
        Slider(multi,"MaxBlockerConcentration",0,1,"Maximum desired blocker reach","Soft cap on the fraction of other paths directly blocked by one arrow.");
        EditorGUILayout.PropertyField(multi.FindPropertyRelative("BlockerDistribution"),new GUIContent("Blocker-count weights","Relative weights for 0,1,2,... blockers. Final bucket includes all larger counts. 2..17 entries."),true);
        var weights=settings.FindPropertyRelative("Weights");
        EditorGUILayout.LabelField("Normalized score weights",EditorStyles.boldLabel);
        foreach(string name in new[] { "EdgeResistance","Dependencies","SafeMoveScarcity","SpatialUnpredictability","CrossFace","VisualComplexity" })
            Slider(weights,name,0,100,ObjectNames.NicifyVariableName(name),"Relative contribution. Weights are normalized; at least one must be positive.");
        serializedObject.ApplyModifiedProperties();
        if(qualityChanged) { Undo.RecordObject(asset,"Apply search quality"); asset.difficultySettings.ApplyQuality(asset.difficultySettings.Quality); EditorUtility.SetDirty(asset); }
        EditorGUILayout.HelpBox($"Effective gate cap: {asset.EffectiveMaxGates}. Requested score: {asset.difficultySettings.TargetScore:F0}/100. Actual measured score and JSON export are available after generation in the controls panel. Unlimited attempts at every difficulty.",MessageType.Info);
    }
    private static void Slider(SerializedProperty parent,string field,float min,float max,string label,string help)
    {
        var property=parent.FindPropertyRelative(field);
        EditorGUI.BeginChangeCheck(); float value=EditorGUILayout.Slider(new GUIContent(label,help),(float)property.doubleValue,min,max);
        if(EditorGUI.EndChangeCheck()) property.doubleValue=value;
    }
}
