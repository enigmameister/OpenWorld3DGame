#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RaceEventDefinition))]
public class RaceEventDefinitionEditor : Editor
{
    SerializedProperty raceMode;

    void OnEnable()
    {
        raceMode = serializedObject.FindProperty("raceMode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader("Core");
        Draw("raceMode");
        Draw("raceRoute");
        Draw("routeArrowGenerator");

        DrawHeader("Start / Finish");
        Draw("raceStartPoint");
        Draw("finishGate");

        CarRaceManager.RaceMode mode =
            (CarRaceManager.RaceMode)raceMode.enumValueIndex;

        switch (mode)
        {
            case CarRaceManager.RaceMode.Circuit:
                DrawHeader("Circuit");
                Draw("totalLaps");
                Draw("splitGates");
                break;

            case CarRaceManager.RaceMode.Sprint:
                DrawHeader("Sprint");
                EditorGUILayout.HelpBox(
                    "Sprint u¿ywa Race Route + Start / Finish. Nie wymaga dodatkowych pól.",
                    MessageType.Info
                );
                break;

            case CarRaceManager.RaceMode.SpeedTrap:
                DrawHeader("Speed Trap");
                Draw("speedTraps");
                Draw("speedTrapVisualRoot");
                break;

            case CarRaceManager.RaceMode.TimeChallenge:
                DrawHeader("Time Challenge");
                Draw("timeChallengeGates");
                Draw("timeChallengeStartTime");
                break;

            case CarRaceManager.RaceMode.Elimination:
                DrawHeader("Elimination");
                Draw("totalLaps");
                Draw("splitGates");
                break;
        }

        DrawHeader("Display");
        Draw("raceDisplayName");
        Draw("raceRouteName");
        Draw("raceLengthKm");
        Draw("raceRewardCash");
        Draw("racePreviewSprite");

        DrawHeader("Save IDs");
        Draw("raceRewardId");
        Draw("raceBestTimeId");

        DrawHeader("World Map Icon");
        Draw("worldMapIconSprite");
        Draw("worldMapIconColor");
        Draw("minimapIconSource");
        Draw("worldMapMarkerPoint");

        serializedObject.ApplyModifiedProperties();
    }

    void DrawHeader(string title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    void Draw(string propertyName)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);

        if (prop != null)
            EditorGUILayout.PropertyField(prop, true);
        else
            EditorGUILayout.HelpBox($"Brak pola: {propertyName}", MessageType.Warning);
    }
}
#endif